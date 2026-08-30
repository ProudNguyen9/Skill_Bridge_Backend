using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Domain.Lecturers;

namespace DNTU.SkillBridge.Application.Academics;

public interface IAcademicEvaluationService
{
    Task<IReadOnlyCollection<AcademicEvaluationResponse>> ListForLecturerAsync(Guid lecturerUserId, CancellationToken cancellationToken);

    Task<AcademicEvaluationResponse?> GetAsync(Guid requesterUserId, Guid projectId, Guid studentId, CancellationToken cancellationToken);

    Task<AcademicEvaluationResponse?> UpsertAsync(
        Guid lecturerUserId,
        Guid projectId,
        Guid studentId,
        UpsertAcademicEvaluationRequest request,
        CancellationToken cancellationToken);

    Task<AcademicEvaluationResponse?> FinalizeAsync(Guid lecturerUserId, Guid projectId, Guid studentId, CancellationToken cancellationToken);
}
