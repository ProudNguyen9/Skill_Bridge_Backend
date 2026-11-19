using System.Text.Json;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Domain.Teams;

namespace DNTU.SkillBridge.Application.Teams;

/// <summary>Student-scoped team, membership, invitation, and recommendation operations.</summary>
public sealed class TeamService(
    ITeamRepository teamRepository,
    IUnitOfWork unitOfWork,
    INotificationWriter? notificationWriter = null) : ITeamService
{
    public async Task<IReadOnlyCollection<TeamResponse>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        return await teamRepository.ListTeamsForStudentAsync(studentId, cancellationToken);
    }

    public async Task<TeamResponse> CreateAsync(Guid userId, CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = new Team(
            request.Name,
            studentId,
            request.Description,
            request.TeamType,
            request.TargetProjectId,
            request.LeaderFunctionalRole,
            request.AvatarUrl);
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

        var nextTargetProjectId = request.TargetProjectId ?? team.TargetProjectId;
        if (await teamRepository.HasActiveProjectOrApplicationAsync(teamId, nextTargetProjectId, cancellationToken))
        {
            return (TeamOperationOutcome.Conflict, null);
        }

        team.UpdateDetails(
            request.Name,
            request.Description ?? team.Description,
            request.TeamType ?? team.TeamType,
            nextTargetProjectId,
            request.AvatarUrl,
            request.AvatarUrl is not null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (TeamOperationOutcome.Success, await teamRepository.ProjectTeamAsync(teamId, cancellationToken));
    }

    public async Task<(TeamOperationOutcome Outcome, TeamResponse? Team)> UpdateAvatarAsync(Guid userId, Guid teamId, string? avatarUrl, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await teamRepository.FindTeamWithMembersAsync(teamId, cancellationToken);
        if (team is null) return (TeamOperationOutcome.NotFound, null);
        if (!IsLeader(team, studentId)) return (TeamOperationOutcome.Forbidden, null);

        team.UpdateAvatar(avatarUrl);
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

        var target = await teamRepository.FindStudentByIdOrIdentifierAsync(request.StudentId, request.Identifier, cancellationToken);
        if (target is null) return (TeamOperationOutcome.NotFound, null);

        var targetStudentId = target.Value.StudentId;
        if (team.Members.Any(item => item.StudentId == targetStudentId) || targetStudentId == studentId)
        {
            return (TeamOperationOutcome.Conflict, null);
        }

        if (await teamRepository.HasPendingInvitationAsync(teamId, targetStudentId, cancellationToken))
        {
            return (TeamOperationOutcome.Conflict, null);
        }

        var invitation = new TeamInvitation(
            teamId,
            targetStudentId,
            studentId,
            request.Role,
            DateTimeOffset.UtcNow.AddDays(request.ExpiresInDays),
            request.FunctionalRole,
            request.Message);
        teamRepository.AddTeamInvitation(invitation);

        notificationWriter?.Add(new NotificationMessage(
            target.Value.UserId,
            "TEAM_INVITATION_RECEIVED",
            $"Bạn nhận được lời mời tham gia nhóm \"{team.Name}\"",
            JsonSerializer.Serialize(new
            {
                teamId = team.Id,
                teamName = team.Name,
                invitationId = invitation.Id,
                functionalRole = request.FunctionalRole,
                message = request.Message
            })));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (TeamOperationOutcome.Success, await teamRepository.ProjectInvitationAsync(invitation.Id, cancellationToken));
    }

    public async Task<IReadOnlyCollection<TeamInvitationResponse>?> ListTeamInvitationsAsync(Guid userId, Guid teamId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var isMember = await teamRepository.HasTeamMembershipAsync(teamId, studentId, cancellationToken);
        if (!isMember) return null;
        return await teamRepository.ListTeamInvitationsAsync(teamId, cancellationToken);
    }

    public async Task<TeamOperationOutcome> RevokeInvitationAsync(Guid userId, Guid teamId, Guid invitationId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await teamRepository.FindTeamWithMembersAsync(teamId, cancellationToken);
        if (team is null) return TeamOperationOutcome.NotFound;
        if (!IsLeader(team, studentId)) return TeamOperationOutcome.Forbidden;
        if (team.IsLocked) return TeamOperationOutcome.Locked;

        var invitation = await teamRepository.FindInvitationByIdAsync(invitationId, cancellationToken);
        if (invitation is null || invitation.TeamId != teamId) return TeamOperationOutcome.NotFound;
        if (invitation.Status != TeamInvitationStatus.PENDING) return TeamOperationOutcome.Conflict;

        invitation.Revoke(DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TeamOperationOutcome.Success;
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
            teamRepository.AddTeamMember(new TeamMember(invitation.TeamId, studentId, invitation.Role, invitation.FunctionalRole));
        }
        else
        {
            invitation.Reject(DateTimeOffset.UtcNow);
        }

        var leaderUserId = await teamRepository.FindStudentUserIdAsync(invitation.InvitedByStudentId, cancellationToken);
        if (leaderUserId.HasValue)
        {
            notificationWriter?.Add(new NotificationMessage(
                leaderUserId.Value,
                accept ? "TEAM_INVITATION_ACCEPTED" : "TEAM_INVITATION_REJECTED",
                accept
                    ? $"Thành viên đã chấp nhận lời mời tham gia nhóm \"{invitation.Team.Name}\""
                    : $"Lời mời tham gia nhóm \"{invitation.Team.Name}\" đã bị từ chối",
                JsonSerializer.Serialize(new { teamId = invitation.TeamId, invitationId = invitation.Id, accepted = accept })));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TeamOperationOutcome.Success;
    }

    public async Task<TeamOperationOutcome> UpdateMemberFunctionalRoleAsync(Guid userId, Guid teamId, Guid memberStudentId, string? functionalRole, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var team = await teamRepository.FindTeamWithMembersAsync(teamId, cancellationToken);
        if (team is null) return TeamOperationOutcome.NotFound;
        if (!IsLeader(team, studentId)) return TeamOperationOutcome.Forbidden;
        if (team.IsLocked) return TeamOperationOutcome.Locked;

        var member = team.Members.SingleOrDefault(item => item.StudentId == memberStudentId);
        if (member is null) return TeamOperationOutcome.NotFound;

        member.AssignFunctionalRole(functionalRole);
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

    public async Task<IReadOnlyCollection<TeamProjectRecommendationResponse>?> ListTeamRecommendationsAsync(Guid userId, Guid teamId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var isMember = await teamRepository.HasTeamMembershipAsync(teamId, studentId, cancellationToken);
        if (!isMember) return null;
        return await teamRepository.ListRecommendationsForTeamAsync(teamId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<TeamProjectRecommendationResponse>> ListMyTeamRecommendationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        return await teamRepository.ListRecommendationsForStudentAsync(studentId, cancellationToken);
    }

    public async Task<TeamOperationOutcome> RespondRecommendationAsync(Guid userId, Guid recommendationId, bool accept, string? note, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        var rec = await teamRepository.FindRecommendationForUpdateAsync(recommendationId, cancellationToken);
        if (rec is null) return TeamOperationOutcome.NotFound;

        var team = await teamRepository.FindTeamWithMembersAsync(rec.TeamId, cancellationToken);
        if (team is null) return TeamOperationOutcome.NotFound;
        if (!IsLeader(team, studentId)) return TeamOperationOutcome.Forbidden;
        if (rec.Status != TeamRecommendationStatus.PENDING) return TeamOperationOutcome.Conflict;

        var now = DateTimeOffset.UtcNow;
        if (accept)
        {
            if (team.IsLocked || await teamRepository.HasActiveProjectOrApplicationAsync(team.Id, rec.ProjectId, cancellationToken))
            {
                return TeamOperationOutcome.Conflict;
            }
            rec.Accept(studentId, now, note);
            if (!team.IsLocked)
            {
                team.SetTargetProject(rec.ProjectId);
            }
        }
        else
        {
            rec.Decline(studentId, now, note);
        }

        var lecturerUserId = await teamRepository.FindLecturerUserIdAsync(rec.LecturerId, cancellationToken);
        if (lecturerUserId.HasValue)
        {
            notificationWriter?.Add(new NotificationMessage(
                lecturerUserId.Value,
                accept ? "TEAM_RECOMMENDATION_ACCEPTED" : "TEAM_RECOMMENDATION_DECLINED",
                accept
                    ? $"Nhóm \"{team.Name}\" đã tiếp nhận lời giới thiệu bài toán của bạn"
                    : $"Nhóm \"{team.Name}\" đã từ chối lời giới thiệu bài toán",
                JsonSerializer.Serialize(new { recommendationId = rec.Id, teamId = rec.TeamId, projectId = rec.ProjectId, accepted = accept, note })));
        }

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
