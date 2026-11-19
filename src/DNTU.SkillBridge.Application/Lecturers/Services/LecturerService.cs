using System.Text.Json;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Application.Teams;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Teams;

namespace DNTU.SkillBridge.Application.Lecturers;

public sealed class LecturerService(
    ILecturerRepository lecturerRepository,
    IUnitOfWork unitOfWork,
    INotificationWriter? notificationWriter = null) : ILecturerService
{
    /// <summary>Returns the caller's own profile, lazily creating it (active, private) on first access.</summary>
    public async Task<LecturerProfileResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        return ProjectProfile(profile);
    }

    /// <summary>Updates the caller's own profile. The lecturer identity always comes from the session.</summary>
    public async Task<LecturerProfileResponse> UpdateMyProfileAsync(Guid userId, UpdateLecturerProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        profile.Update(
            request.LecturerCode,
            request.Department,
            request.AcademicTitle,
            request.Bio,
            request.WebsiteUrl,
            request.OfficeLocation,
            request.PhoneNumber,
            request.IsProfilePublic,
            request.ShowContactInfo,
            request.AvatarUrl);

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            await lecturerRepository.UpdateUserDisplayNameAsync(userId, request.DisplayName, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ProjectProfile(profile);
    }

    /// <summary>Lists the caller's project supervision assignments, enriched with project/team metadata.</summary>
    public async Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetMyAssignmentsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        return await lecturerRepository.ListEnrichedAssignmentsAsync(profile.Id, cancellationToken);
    }

    /// <summary>The caller accepts an invited supervision assignment as their own.</summary>
    public async Task<(LecturerAssignmentAcceptOutcome Outcome, LecturerAssignmentResponse? Assignment)> AcceptMyAssignmentAsync(
        Guid userId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        var assignment = await lecturerRepository.FindAssignmentForUpdateAsync(assignmentId, profile.Id, cancellationToken);
        if (assignment is null)
        {
            return (LecturerAssignmentAcceptOutcome.NotFound, null);
        }

        if (assignment.Status != LecturerAssignmentStatus.INVITED)
        {
            return (LecturerAssignmentAcceptOutcome.NotOpenForAcceptance, null);
        }

        assignment.Accept(DateTimeOffset.UtcNow);

        var notifyTargets = (await lecturerRepository.ListProjectParticipantUserIdsAsync(assignment.ProjectId, cancellationToken))
            .Concat(assignment.InvitedByUserId.HasValue ? [assignment.InvitedByUserId.Value] : [])
            .Where(id => id != userId)
            .Distinct();
        foreach (var targetUserId in notifyTargets)
        {
            notificationWriter?.Add(new NotificationMessage(
                targetUserId,
                "LECTURER_SUPERVISION_ACCEPTED",
                "Giảng viên đã chấp nhận lời mời hướng dẫn kỹ thuật",
                JsonSerializer.Serialize(new { assignmentId = assignment.Id, projectId = assignment.ProjectId, lecturerId = profile.Id })));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (LecturerAssignmentAcceptOutcome.Accepted, await lecturerRepository.ProjectAssignmentAsync(assignment.Id, cancellationToken) ?? ProjectAssignment(assignment));
    }

    /// <summary>The caller declines an invited supervision assignment with an optional reason.</summary>
    public async Task<(LecturerAssignmentAcceptOutcome Outcome, LecturerAssignmentResponse? Assignment)> DeclineMyAssignmentAsync(
        Guid userId, Guid assignmentId, string? reason, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        var assignment = await lecturerRepository.FindAssignmentForUpdateAsync(assignmentId, profile.Id, cancellationToken);
        if (assignment is null)
        {
            return (LecturerAssignmentAcceptOutcome.NotFound, null);
        }

        if (assignment.Status != LecturerAssignmentStatus.INVITED)
        {
            return (LecturerAssignmentAcceptOutcome.NotOpenForAcceptance, null);
        }

        assignment.Decline(DateTimeOffset.UtcNow, reason);

        if (assignment.InvitedByUserId.HasValue && assignment.InvitedByUserId.Value != userId)
        {
            notificationWriter?.Add(new NotificationMessage(
                assignment.InvitedByUserId.Value,
                "LECTURER_SUPERVISION_DECLINED",
                "Giảng viên đã từ chối lời mời hướng dẫn kỹ thuật",
                JsonSerializer.Serialize(new { assignmentId = assignment.Id, projectId = assignment.ProjectId, lecturerId = profile.Id, reason })));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (LecturerAssignmentAcceptOutcome.Accepted, await lecturerRepository.ProjectAssignmentAsync(assignment.Id, cancellationToken) ?? ProjectAssignment(assignment));
    }

    /// <summary>Admin projection of one lecturer's supervision assignments.</summary>
    public async Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetLecturerAssignmentsAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        await lecturerRepository.ListEnrichedAssignmentsAsync(lecturerId, cancellationToken);

    /// <summary>
    /// Admin: Invites a lecturer to supervise a project (status INVITED).
    /// </summary>
    public async Task<(LecturerAssignmentCreateOutcome Outcome, LecturerAssignmentResponse? Assignment)> CreateAssignmentAsync(
        Guid lecturerId, CreateLecturerAssignmentRequest request, CancellationToken cancellationToken)
    {
        var profile = await lecturerRepository.FindProfileForUpdateAsync(lecturerId, cancellationToken);
        if (profile is null)
        {
            return (LecturerAssignmentCreateOutcome.LecturerNotFound, null);
        }

        if (!profile.CanTakeAssignments())
        {
            return (LecturerAssignmentCreateOutcome.LecturerInactive, null);
        }

        if (await lecturerRepository.HasAssignmentForProjectAsync(request.ProjectId, cancellationToken))
        {
            return (LecturerAssignmentCreateOutcome.DuplicateAssignment, null);
        }

        var assignment = profile.AssignProject(request.ProjectId, request.Role, request.Note);
        lecturerRepository.AddAssignment(assignment);
        notificationWriter?.Add(new NotificationMessage(
            profile.UserId,
            "LECTURER_SUPERVISION_INVITED",
            "Bạn được mời hướng dẫn kỹ thuật cho một đề tài dự án",
            JsonSerializer.Serialize(new { assignmentId = assignment.Id, projectId = request.ProjectId, note = request.Note })));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (LecturerAssignmentCreateOutcome.Created, await lecturerRepository.ProjectAssignmentAsync(assignment.Id, cancellationToken) ?? ProjectAssignment(assignment));
    }

    /// <summary>Removes a supervision assignment; returns false when it does not exist.</summary>
    public async Task<bool> DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await lecturerRepository.FindAssignmentAsync(assignmentId, cancellationToken);
        if (assignment is null)
        {
            return false;
        }

        lecturerRepository.RemoveAssignment(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<IReadOnlyCollection<LecturerDirectoryItemResponse>> SearchDirectoryAsync(string? query, int limit, CancellationToken cancellationToken) =>
        lecturerRepository.SearchDirectoryAsync(query, Math.Clamp(limit, 1, 50), cancellationToken);

    public async Task<(ProjectLecturerInviteOutcome Outcome, LecturerAssignmentResponse? Assignment)> InviteLecturerToProjectAsync(
        Guid actorUserId, bool isAdmin, Guid projectId, InviteProjectLecturerRequest request, CancellationToken cancellationToken)
    {
        if (!await lecturerRepository.ProjectExistsAsync(projectId, cancellationToken))
        {
            return (ProjectLecturerInviteOutcome.ProjectNotFound, null);
        }

        if (!isAdmin && !await lecturerRepository.CanUserInviteLecturerForProjectAsync(actorUserId, projectId, cancellationToken))
        {
            return (ProjectLecturerInviteOutcome.Forbidden, null);
        }

        var profile = await lecturerRepository.FindProfileForUpdateAsync(request.LecturerId, cancellationToken);
        if (profile is null)
        {
            return (ProjectLecturerInviteOutcome.LecturerNotFound, null);
        }

        if (!profile.CanTakeAssignments())
        {
            return (ProjectLecturerInviteOutcome.LecturerInactive, null);
        }

        if (await lecturerRepository.HasAssignmentForProjectAsync(projectId, cancellationToken))
        {
            return (ProjectLecturerInviteOutcome.DuplicateAssignment, null);
        }

        var assignment = profile.AssignProject(projectId, request.Role, request.Note);
        assignment.SetInvitationDetails(
            actorUserId,
            request.TeamId,
            request.ExpectedWork,
            request.ExpectedDuration,
            request.AllowanceShareAmount,
            request.IsCourseLinked,
            request.CourseName);

        lecturerRepository.AddAssignment(assignment);
        notificationWriter?.Add(new NotificationMessage(
            profile.UserId,
            "LECTURER_SUPERVISION_INVITED",
            "Nhóm sinh viên đã gửi lời mời bạn hướng dẫn kỹ thuật cho dự án",
            JsonSerializer.Serialize(new
            {
                assignmentId = assignment.Id,
                projectId,
                teamId = request.TeamId,
                expectedWork = request.ExpectedWork,
                expectedDuration = request.ExpectedDuration,
                allowanceShareAmount = request.AllowanceShareAmount,
                isCourseLinked = request.IsCourseLinked,
                courseName = request.CourseName
            })));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (ProjectLecturerInviteOutcome.Created, await lecturerRepository.ProjectAssignmentAsync(assignment.Id, cancellationToken) ?? ProjectAssignment(assignment));
    }

    public Task<IReadOnlyCollection<LecturerAssignmentResponse>> ListProjectAssignmentsAsync(Guid projectId, CancellationToken cancellationToken) =>
        lecturerRepository.ListProjectAssignmentsAsync(projectId, cancellationToken);

    public Task<IReadOnlyCollection<LecturerAssignmentResponse>> ListSentProjectInvitationsAsync(Guid userId, CancellationToken cancellationToken) =>
        lecturerRepository.ListSentProjectInvitationsAsync(userId, cancellationToken);

    public async Task<bool> CancelProjectInvitationAsync(Guid actorUserId, bool isAdmin, Guid projectId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await lecturerRepository.FindAssignmentAsync(assignmentId, cancellationToken);
        if (assignment is null || assignment.ProjectId != projectId || assignment.Status != LecturerAssignmentStatus.INVITED)
        {
            return false;
        }

        if (!isAdmin && assignment.InvitedByUserId != actorUserId &&
            !await lecturerRepository.CanUserInviteLecturerForProjectAsync(actorUserId, projectId, cancellationToken))
        {
            return false;
        }

        lecturerRepository.RemoveAssignment(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<IReadOnlyCollection<LecturerTeamItemResponse>> ListTeamsForLecturerAsync(string? query, CancellationToken cancellationToken) =>
        lecturerRepository.ListTeamsForLecturerAsync(query, cancellationToken);

    public async Task<(LecturerRecommendationCreateOutcome Outcome, TeamProjectRecommendationResponse? Recommendation)> RecommendTeamToProjectAsync(
        Guid lecturerUserId, CreateTeamRecommendationRequest request, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(lecturerUserId, cancellationToken);
        if (!profile.CanTakeAssignments())
        {
            return (LecturerRecommendationCreateOutcome.LecturerNotApproved, null);
        }

        var teamInfo = await lecturerRepository.FindTeamLeaderInfoAsync(request.TeamId, cancellationToken);
        if (teamInfo is null)
        {
            return (LecturerRecommendationCreateOutcome.TeamNotFound, null);
        }

        if (await lecturerRepository.IsTeamBusyWithAnotherProjectAsync(request.TeamId, cancellationToken))
        {
            return (LecturerRecommendationCreateOutcome.DuplicatePending, null);
        }

        if (!await lecturerRepository.ProjectExistsAsync(request.ProjectId, cancellationToken))
        {
            return (LecturerRecommendationCreateOutcome.ProjectNotFound, null);
        }

        if (await lecturerRepository.HasPendingRecommendationAsync(profile.Id, request.TeamId, request.ProjectId, cancellationToken))
        {
            return (LecturerRecommendationCreateOutcome.DuplicatePending, null);
        }

        var rec = new TeamProjectRecommendation(profile.Id, request.TeamId, request.ProjectId, request.Reason);
        lecturerRepository.AddRecommendation(rec);
        notificationWriter?.Add(new NotificationMessage(
            teamInfo.Value.LeaderUserId,
            "LECTURER_TEAM_RECOMMENDED",
            $"Giảng viên đã giới thiệu bài toán phù hợp cho nhóm \"{teamInfo.Value.TeamName}\"",
            JsonSerializer.Serialize(new
            {
                recommendationId = rec.Id,
                teamId = request.TeamId,
                projectId = request.ProjectId,
                reason = request.Reason
            })));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var items = await lecturerRepository.ListLecturerRecommendationsAsync(profile.Id, cancellationToken);
        return (LecturerRecommendationCreateOutcome.Created, items.FirstOrDefault(x => x.Id == rec.Id));
    }

    public async Task<IReadOnlyCollection<TeamProjectRecommendationResponse>> ListMyRecommendationsAsync(Guid lecturerUserId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(lecturerUserId, cancellationToken);
        return await lecturerRepository.ListLecturerRecommendationsAsync(profile.Id, cancellationToken);
    }

    private async Task<LecturerProfile> EnsureProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Tracked on purpose: the context defaults to NoTracking and /me writes mutate this entity.
        var profile = await lecturerRepository.FindProfileByUserIdAsync(userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = new LecturerProfile(userId);
        // Lecturer reaching /lecturers/me comes from admin-provisioned or verified Google staff accounts.
        profile.Approve(DateTimeOffset.UtcNow);
        lecturerRepository.AddProfile(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private static LecturerProfileResponse ProjectProfile(LecturerProfile profile) =>
        new(profile.Id,
            profile.LecturerCode,
            profile.Department,
            profile.AcademicTitle,
            profile.Bio,
            profile.WebsiteUrl,
            profile.OfficeLocation,
            profile.PhoneNumber,
            profile.IsProfilePublic,
            profile.ShowContactInfo,
            profile.IsActive,
            profile.AvatarUrl);

    private static LecturerAssignmentResponse ProjectAssignment(LecturerAssignment assignment) =>
        new(assignment.Id,
            assignment.LecturerId,
            assignment.ProjectId,
            assignment.Role.ToString(),
            assignment.Status.ToString(),
            assignment.AssignedAt,
            assignment.AcceptedAt,
            assignment.EndedAt,
            assignment.Note,
            assignment.TeamId,
            null,
            assignment.InvitedByUserId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            assignment.ExpectedWork,
            assignment.ExpectedDuration,
            assignment.AllowanceShareAmount,
            assignment.IsCourseLinked,
            assignment.CourseName,
            assignment.RespondedAt,
            assignment.DeclineReason);
}
