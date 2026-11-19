using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Teams;
using DNTU.SkillBridge.Domain.Lecturers;

namespace DNTU.SkillBridge.Application.Lecturers;

public interface ILecturerService
{
    Task<LecturerProfileResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<LecturerProfileResponse> UpdateMyProfileAsync(Guid userId, UpdateLecturerProfileRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetMyAssignmentsAsync(Guid userId, CancellationToken cancellationToken);

    Task<(LecturerAssignmentAcceptOutcome Outcome, LecturerAssignmentResponse? Assignment)> AcceptMyAssignmentAsync(
        Guid userId, Guid assignmentId, CancellationToken cancellationToken);

    Task<(LecturerAssignmentAcceptOutcome Outcome, LecturerAssignmentResponse? Assignment)> DeclineMyAssignmentAsync(
        Guid userId, Guid assignmentId, string? reason, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetLecturerAssignmentsAsync(Guid lecturerId, CancellationToken cancellationToken);

    Task<(LecturerAssignmentCreateOutcome Outcome, LecturerAssignmentResponse? Assignment)> CreateAssignmentAsync(
        Guid lecturerId, CreateLecturerAssignmentRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerDirectoryItemResponse>> SearchDirectoryAsync(string? query, int limit, CancellationToken cancellationToken);

    Task<(ProjectLecturerInviteOutcome Outcome, LecturerAssignmentResponse? Assignment)> InviteLecturerToProjectAsync(
        Guid actorUserId, bool isAdminOrCompanyBypass, Guid projectId, InviteProjectLecturerRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerAssignmentResponse>> ListProjectAssignmentsAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerAssignmentResponse>> ListSentProjectInvitationsAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> CancelProjectInvitationAsync(Guid actorUserId, bool isAdmin, Guid projectId, Guid assignmentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerTeamItemResponse>> ListTeamsForLecturerAsync(string? query, CancellationToken cancellationToken);

    Task<(LecturerRecommendationCreateOutcome Outcome, TeamProjectRecommendationResponse? Recommendation)> RecommendTeamToProjectAsync(
        Guid lecturerUserId, CreateTeamRecommendationRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TeamProjectRecommendationResponse>> ListMyRecommendationsAsync(Guid lecturerUserId, CancellationToken cancellationToken);
}
