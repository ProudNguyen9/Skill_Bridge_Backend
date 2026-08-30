using DNTU.SkillBridge.Domain.Teams;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Teams;

public enum TeamOperationOutcome
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    Locked,
    Invalid
}

/// <summary>Student-scoped team, membership, and invitation operations.</summary>
public sealed class TeamService(AppDbContext dbContext)
{
    public async Task<TeamResponse> CreateAsync(Guid userId, CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = new Team(request.Name, studentId);
        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ProjectAsync(team.Id, cancellationToken) ?? throw new InvalidOperationException("The new team was not found.");
    }

    public async Task<TeamResponse?> GetAsync(Guid userId, Guid teamId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var isMember = await dbContext.TeamMembers.AsNoTracking()
            .AnyAsync(member => member.TeamId == teamId && member.StudentId == studentId, cancellationToken);
        return isMember ? await ProjectAsync(teamId, cancellationToken) : null;
    }

    public async Task<(TeamOperationOutcome Outcome, TeamResponse? Team)> UpdateAsync(Guid userId, Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await dbContext.Teams.Include(item => item.Members).SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null) return (TeamOperationOutcome.NotFound, null);
        if (!IsLeader(team, studentId)) return (TeamOperationOutcome.Forbidden, null);
        if (team.IsLocked) return (TeamOperationOutcome.Locked, null);

        team.Rename(request.Name);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (TeamOperationOutcome.Success, await ProjectAsync(teamId, cancellationToken));
    }

    public async Task<TeamOperationOutcome> DeleteAsync(Guid userId, Guid teamId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await dbContext.Teams.Include(item => item.Members).SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null) return TeamOperationOutcome.NotFound;
        if (!IsLeader(team, studentId)) return TeamOperationOutcome.Forbidden;
        if (team.IsLocked) return TeamOperationOutcome.Locked;

        dbContext.Teams.Remove(team);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TeamOperationOutcome.Success;
    }

    public async Task<(TeamOperationOutcome Outcome, TeamInvitationResponse? Invitation)> InviteAsync(Guid userId, Guid teamId, InviteTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await dbContext.Teams.Include(item => item.Members).SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null) return (TeamOperationOutcome.NotFound, null);
        if (!IsLeader(team, studentId)) return (TeamOperationOutcome.Forbidden, null);
        if (team.IsLocked) return (TeamOperationOutcome.Locked, null);
        if (team.Members.Any(item => item.StudentId == request.StudentId) || request.StudentId == studentId) return (TeamOperationOutcome.Conflict, null);
        if (!await dbContext.StudentProfiles.AsNoTracking().AnyAsync(item => item.Id == request.StudentId, cancellationToken)) return (TeamOperationOutcome.NotFound, null);
        if (await dbContext.TeamInvitations.AnyAsync(item => item.TeamId == teamId && item.InvitedStudentId == request.StudentId && item.Status == TeamInvitationStatus.PENDING, cancellationToken)) return (TeamOperationOutcome.Conflict, null);

        var invitation = new TeamInvitation(teamId, request.StudentId, studentId, request.Role, DateTimeOffset.UtcNow.AddDays(request.ExpiresInDays));
        dbContext.TeamInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (TeamOperationOutcome.Success, await InvitationAsync(invitation.Id, cancellationToken));
    }

    public async Task<IReadOnlyCollection<TeamInvitationResponse>> ListMyInvitationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var expired = await dbContext.TeamInvitations.Where(item => item.InvitedStudentId == studentId && item.Status == TeamInvitationStatus.PENDING && item.ExpiresAt <= now).ToListAsync(cancellationToken);
        foreach (var invitation in expired) invitation.Expire(now);
        if (expired.Count != 0) await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.TeamInvitations.AsNoTracking().Where(item => item.InvitedStudentId == studentId)
            .OrderByDescending(item => item.CreatedAt).Select(item => new TeamInvitationResponse(item.Id, item.TeamId, item.Team.Name, item.InvitedStudentId, item.InvitedByStudentId, item.Role, item.Status, item.ExpiresAt, item.CreatedAt)).ToListAsync(cancellationToken);
    }

    public async Task<TeamOperationOutcome> RespondAsync(Guid userId, Guid invitationId, bool accept, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var invitation = await dbContext.TeamInvitations.Include(item => item.Team).ThenInclude(item => item.Members).SingleOrDefaultAsync(item => item.Id == invitationId && item.InvitedStudentId == studentId, cancellationToken);
        if (invitation is null) return TeamOperationOutcome.NotFound;
        if (invitation.Team.IsLocked) return TeamOperationOutcome.Locked;
        if (!invitation.IsPendingAndUnexpired(DateTimeOffset.UtcNow))
        {
            invitation.Expire(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            return TeamOperationOutcome.Conflict;
        }
        if (accept)
        {
            if (invitation.Team.Members.Any(item => item.StudentId == studentId)) return TeamOperationOutcome.Conflict;
            invitation.Accept(DateTimeOffset.UtcNow);
            dbContext.TeamMembers.Add(new TeamMember(invitation.TeamId, studentId, invitation.Role));
        }
        else invitation.Reject(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TeamOperationOutcome.Success;
    }

    public async Task<TeamOperationOutcome> RemoveMemberAsync(Guid userId, Guid teamId, Guid memberStudentId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await dbContext.Teams.Include(item => item.Members).SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null) return TeamOperationOutcome.NotFound;
        if (!IsLeader(team, studentId)) return TeamOperationOutcome.Forbidden;
        if (team.IsLocked) return TeamOperationOutcome.Locked;
        var member = team.Members.SingleOrDefault(item => item.StudentId == memberStudentId);
        if (member is null) return TeamOperationOutcome.NotFound;
        if (member.Role == TeamMemberRole.LEADER || team.Members.Count <= 1) return TeamOperationOutcome.Conflict;
        dbContext.TeamMembers.Remove(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TeamOperationOutcome.Success;
    }

    public async Task<TeamOperationOutcome> ChangeLeaderAsync(Guid userId, Guid teamId, Guid newLeaderStudentId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await dbContext.Teams.Include(item => item.Members).SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null) return TeamOperationOutcome.NotFound;
        if (!IsLeader(team, studentId)) return TeamOperationOutcome.Forbidden;
        if (team.IsLocked) return TeamOperationOutcome.Locked;
        var newLeader = team.Members.SingleOrDefault(item => item.StudentId == newLeaderStudentId);
        var oldLeader = team.Members.Single(item => item.Role == TeamMemberRole.LEADER);
        if (newLeader is null) return TeamOperationOutcome.NotFound;
        if (newLeader == oldLeader) return TeamOperationOutcome.Success;
        oldLeader.AssignRole(TeamMemberRole.MEMBER);
        newLeader.AssignRole(TeamMemberRole.LEADER);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TeamOperationOutcome.Success;
    }

    private async Task<Guid> ResolveStudentIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var id = await dbContext.StudentProfiles.AsNoTracking().Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        if (id.HasValue) return id.Value;
        var profile = new DNTU.SkillBridge.Domain.Students.StudentProfile(userId);
        dbContext.StudentProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile.Id;
    }

    private static bool IsLeader(Team team, Guid studentId) => team.Members.Any(item => item.StudentId == studentId && item.Role == TeamMemberRole.LEADER);

    private Task<TeamResponse?> ProjectAsync(Guid teamId, CancellationToken cancellationToken) => dbContext.Teams.AsNoTracking().Where(item => item.Id == teamId).Select(item => new TeamResponse(item.Id, item.Name, item.CreatedByStudentId, item.IsLocked, item.Members.OrderBy(member => member.Role).Select(member => new TeamMemberResponse(member.StudentId, member.Role)).ToList(), item.CreatedAt)).SingleOrDefaultAsync(cancellationToken);

    private Task<TeamInvitationResponse?> InvitationAsync(Guid invitationId, CancellationToken cancellationToken) => dbContext.TeamInvitations.AsNoTracking().Where(item => item.Id == invitationId).Select(item => new TeamInvitationResponse(item.Id, item.TeamId, item.Team.Name, item.InvitedStudentId, item.InvitedByStudentId, item.Role, item.Status, item.ExpiresAt, item.CreatedAt)).SingleOrDefaultAsync(cancellationToken);
}
