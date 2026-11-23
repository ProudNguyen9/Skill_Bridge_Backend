using DNTU.SkillBridge.Application.Teams;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Teams;

namespace DNTU.SkillBridge.Application.Lecturers;

/// <summary>Data operations for lecturer profiles and project supervision assignments.</summary>
public interface ILecturerRepository
{
    /// <summary>Reads the lecturer profile owned by a user as a tracked entity so /me writes mutate it.</summary>
    Task<LecturerProfile?> FindProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Reads a lecturer profile by id as a tracked entity so assignment writes mutate it.</summary>
    Task<LecturerProfile?> FindProfileForUpdateAsync(Guid lecturerId, CancellationToken cancellationToken);

    Task UpdateUserDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken);
    Task<string?> GetLecturerDisplayNameAsync(Guid userId, CancellationToken cancellationToken);
    Task<string?> GetProjectTitleAsync(Guid projectId, CancellationToken cancellationToken);

    void AddProfile(LecturerProfile profile);

    void AddAssignment(LecturerAssignment assignment);

    /// <summary>Reads a supervision assignment owned by a lecturer as a tracked entity so mutations are persisted.</summary>
    Task<LecturerAssignment?> FindAssignmentForUpdateAsync(Guid assignmentId, Guid lecturerId, CancellationToken cancellationToken);

    /// <summary>Reads a supervision assignment by id with the context's default tracking behavior.</summary>
    Task<LecturerAssignment?> FindAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);

    void RemoveAssignment(LecturerAssignment assignment);

    Task<bool> HasAssignmentForProjectAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Lists a lecturer's supervision assignments detached, oldest first.</summary>
    Task<IReadOnlyCollection<LecturerAssignment>> ListAssignmentsAsync(Guid lecturerId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerAssignmentResponse>> ListEnrichedAssignmentsAsync(Guid lecturerId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerAssignmentResponse>> ListProjectAssignmentsAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerAssignmentResponse>> ListSentProjectInvitationsAsync(Guid userId, CancellationToken cancellationToken);

    Task<LecturerAssignmentResponse?> ProjectAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerDirectoryItemResponse>> SearchDirectoryAsync(string? query, int limit, CancellationToken cancellationToken);

    Task<bool> ProjectExistsAsync(Guid projectId, CancellationToken cancellationToken);

    Task<bool> CanUserInviteLecturerForProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> ListProjectParticipantUserIdsAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerTeamItemResponse>> ListTeamsForLecturerAsync(string? query, CancellationToken cancellationToken);

    Task<(Guid LeaderUserId, string TeamName)?> FindTeamLeaderInfoAsync(Guid teamId, CancellationToken cancellationToken);

    Task<bool> IsTeamBusyWithAnotherProjectAsync(Guid teamId, CancellationToken cancellationToken);

    Task<bool> HasPendingRecommendationAsync(Guid lecturerId, Guid teamId, Guid projectId, CancellationToken cancellationToken);

    void AddRecommendation(TeamProjectRecommendation recommendation);

    Task<IReadOnlyCollection<TeamProjectRecommendationResponse>> ListLecturerRecommendationsAsync(Guid lecturerId, CancellationToken cancellationToken);
}
