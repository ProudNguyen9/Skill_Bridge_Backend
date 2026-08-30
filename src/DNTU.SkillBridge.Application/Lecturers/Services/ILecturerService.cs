using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Lecturers;

namespace DNTU.SkillBridge.Application.Lecturers;

public interface ILecturerService
{
    Task<LecturerProfileResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<LecturerProfileResponse> UpdateMyProfileAsync(Guid userId, UpdateLecturerProfileRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetMyAssignmentsAsync(Guid userId, CancellationToken cancellationToken);

    Task<(LecturerAssignmentAcceptOutcome Outcome, LecturerAssignmentResponse? Assignment)> AcceptMyAssignmentAsync(
        Guid userId, Guid assignmentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetLecturerAssignmentsAsync(Guid lecturerId, CancellationToken cancellationToken);

    Task<(LecturerAssignmentCreateOutcome Outcome, LecturerAssignmentResponse? Assignment)> CreateAssignmentAsync(
        Guid lecturerId, CreateLecturerAssignmentRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);
}
