using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Domain.Teams;

namespace DNTU.SkillBridge.Application.Teams;

public interface ITeamService
{
    Task<TeamResponse> CreateAsync(Guid userId, CreateTeamRequest request, CancellationToken cancellationToken);

    Task<TeamResponse?> GetAsync(Guid userId, Guid teamId, CancellationToken cancellationToken);

    Task<(TeamOperationOutcome Outcome, TeamResponse? Team)> UpdateAsync(Guid userId, Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken);

    Task<TeamOperationOutcome> DeleteAsync(Guid userId, Guid teamId, CancellationToken cancellationToken);

    Task<(TeamOperationOutcome Outcome, TeamInvitationResponse? Invitation)> InviteAsync(Guid userId, Guid teamId, InviteTeamMemberRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TeamInvitationResponse>> ListMyInvitationsAsync(Guid userId, CancellationToken cancellationToken);

    Task<TeamOperationOutcome> RespondAsync(Guid userId, Guid invitationId, bool accept, CancellationToken cancellationToken);

    Task<TeamOperationOutcome> RemoveMemberAsync(Guid userId, Guid teamId, Guid memberStudentId, CancellationToken cancellationToken);

    Task<TeamOperationOutcome> ChangeLeaderAsync(Guid userId, Guid teamId, Guid newLeaderStudentId, CancellationToken cancellationToken);
}

/// <summary>Student-scoped team, membership, and invitation operations.</summary>
public sealed class TeamService(ITeamRepository teamRepository, IUnitOfWork unitOfWork) : ITeamService
{
    public async Task<TeamResponse> CreateAsync(Guid userId, CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = new Team(request.Name, studentId);
        teamRepository.AddTeam(team);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await teamRepository.ProjectTeamAsync(team.Id, cancellationToken) ?? throw new InvalidOperationException("The new team was not found.");
    }

    public async Task<TeamResponse?> GetAsync(Guid userId, Guid teamId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var isMember = await teamRepository.HasTeamMembershipAsync(teamId, studentId, cancellationToken);
        return isMember ? await teamRepository.ProjectTeamAsync(teamId, cancellationToken) : null;
    }

    public async Task<(TeamOperationOutcome Outcome, TeamResponse? Team)> UpdateAsync(Guid userId, Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await teamRepository.FindTeamWithMembersAsync(teamId, cancellationToken);
        if (team is null) return (TeamOperationOutcome.NotFound, null);
        if (!IsLeader(team, studentId)) return (TeamOperationOutcome.Forbidden, null);
        if (team.IsLocked) return (TeamOperationOutcome.Locked, null);

        team.Rename(request.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (TeamOperationOutcome.Success, await teamRepository.ProjectTeamAsync(teamId, cancellationToken));
    }

    public async Task<TeamOperationOutcome> DeleteAsync(Guid userId, Guid teamId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await teamRepository.FindTeamWithMembersAsync(teamId, cancellationToken);
        if (team is null) return TeamOperationOutcome.NotFound;
        if (!IsLeader(team, studentId)) return TeamOperationOutcome.Forbidden;
        if (team.IsLocked) return TeamOperationOutcome.Locked;

        teamRepository.RemoveTeam(team);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TeamOperationOutcome.Success;
    }

    public async Task<(TeamOperationOutcome Outcome, TeamInvitationResponse? Invitation)> InviteAsync(Guid userId, Guid teamId, InviteTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await teamRepository.FindTeamWithMembersAsync(teamId, cancellationToken);
        if (team is null) return (TeamOperationOutcome.NotFound, null);
        if (!IsLeader(team, studentId)) return (TeamOperationOutcome.Forbidden, null);
        if (team.IsLocked) return (TeamOperationOutcome.Locked, null);
        if (team.Members.Any(item => item.StudentId == request.StudentId) || request.StudentId == studentId) return (TeamOperationOutcome.Conflict, null);
        if (!await teamRepository.HasStudentProfileAsync(request.StudentId, cancellationToken)) return (TeamOperationOutcome.NotFound, null);
        if (await teamRepository.HasPendingInvitationAsync(teamId, request.StudentId, cancellationToken)) return (TeamOperationOutcome.Conflict, null);

        var invitation = new TeamInvitation(teamId, request.StudentId, studentId, request.Role, DateTimeOffset.UtcNow.AddDays(request.ExpiresInDays));
        teamRepository.AddTeamInvitation(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (TeamOperationOutcome.Success, await teamRepository.ProjectInvitationAsync(invitation.Id, cancellationToken));
    }

    public async Task<IReadOnlyCollection<TeamInvitationResponse>> ListMyInvitationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var expired = await teamRepository.FindPendingInvitationsAsync(studentId, now, cancellationToken);
        foreach (var invitation in expired) invitation.Expire(now);
        if (expired.Count != 0) await unitOfWork.SaveChangesAsync(cancellationToken);

        return await teamRepository.ListInvitationsAsync(studentId, cancellationToken);
    }

    public async Task<TeamOperationOutcome> RespondAsync(Guid userId, Guid invitationId, bool accept, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var invitation = await teamRepository.FindInvitationForStudentAsync(invitationId, studentId, cancellationToken);
        if (invitation is null) return TeamOperationOutcome.NotFound;
        if (invitation.Team.IsLocked) return TeamOperationOutcome.Locked;
        if (!invitation.IsPendingAndUnexpired(DateTimeOffset.UtcNow))
        {
            invitation.Expire(DateTimeOffset.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return TeamOperationOutcome.Conflict;
        }
        if (accept)
        {
            if (invitation.Team.Members.Any(item => item.StudentId == studentId)) return TeamOperationOutcome.Conflict;
            invitation.Accept(DateTimeOffset.UtcNow);
            teamRepository.AddTeamMember(new TeamMember(invitation.TeamId, studentId, invitation.Role));
        }
        else invitation.Reject(DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TeamOperationOutcome.Success;
    }

    public async Task<TeamOperationOutcome> RemoveMemberAsync(Guid userId, Guid teamId, Guid memberStudentId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await teamRepository.FindTeamWithMembersAsync(teamId, cancellationToken);
        if (team is null) return TeamOperationOutcome.NotFound;
        if (!IsLeader(team, studentId)) return TeamOperationOutcome.Forbidden;
        if (team.IsLocked) return TeamOperationOutcome.Locked;
        var member = team.Members.SingleOrDefault(item => item.StudentId == memberStudentId);
        if (member is null) return TeamOperationOutcome.NotFound;
        if (member.Role == TeamMemberRole.LEADER || team.Members.Count <= 1) return TeamOperationOutcome.Conflict;
        teamRepository.RemoveTeamMember(member);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TeamOperationOutcome.Success;
    }

    public async Task<TeamOperationOutcome> ChangeLeaderAsync(Guid userId, Guid teamId, Guid newLeaderStudentId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await teamRepository.FindTeamWithMembersAsync(teamId, cancellationToken);
        if (team is null) return TeamOperationOutcome.NotFound;
        if (!IsLeader(team, studentId)) return TeamOperationOutcome.Forbidden;
        if (team.IsLocked) return TeamOperationOutcome.Locked;
        var newLeader = team.Members.SingleOrDefault(item => item.StudentId == newLeaderStudentId);
        var oldLeader = team.Members.Single(item => item.Role == TeamMemberRole.LEADER);
        if (newLeader is null) return TeamOperationOutcome.NotFound;
        if (newLeader == oldLeader) return TeamOperationOutcome.Success;
        oldLeader.AssignRole(TeamMemberRole.MEMBER);
        newLeader.AssignRole(TeamMemberRole.LEADER);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TeamOperationOutcome.Success;
    }

    private async Task<Guid> ResolveStudentIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var id = await teamRepository.FindStudentProfileIdAsync(userId, cancellationToken);
        if (id.HasValue) return id.Value;
        var profile = new StudentProfile(userId);
        teamRepository.AddStudentProfile(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile.Id;
    }

    private static bool IsLeader(Team team, Guid studentId) => team.Members.Any(item => item.StudentId == studentId && item.Role == TeamMemberRole.LEADER);
}
