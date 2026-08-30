using DNTU.SkillBridge.Domain.Academics;

namespace DNTU.SkillBridge.Application.Academics;

/// <summary>Data operations for academic evaluations and their criterion scores.</summary>
public interface IAcademicEvaluationRepository
{
    Task<IReadOnlyCollection<AcademicEvaluation>> ListForLecturerAsync(Guid lecturerUserId, CancellationToken cancellationToken);

    Task<AcademicEvaluation?> FindEvaluationAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken);

    /// <summary>Reads an evaluation with its scores with the context's default tracking behavior.</summary>
    Task<AcademicEvaluation?> FindEvaluationWithScoresAsync(Guid projectId, Guid studentId, Guid rubricId, CancellationToken cancellationToken);

    /// <summary>Reads an evaluation with the context's default tracking behavior.</summary>
    Task<AcademicEvaluation?> FindEvaluationForUpdateAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken);

    void AddEvaluation(AcademicEvaluation evaluation);

    Task<bool> IsAssignedActiveLecturerAsync(Guid lecturerUserId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> CanLecturerReadProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> HasActiveProjectMemberAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken);

    Task<bool> HasActiveMemberForUserAsync(Guid projectId, Guid studentId, Guid userId, CancellationToken cancellationToken);

    Task<Rubric?> FindLockedRubricWithCriteriaAsync(Guid rubricId, CancellationToken cancellationToken);

    Task<bool> HasApprovedCourseProjectAsync(Guid projectId, Guid rubricId, CancellationToken cancellationToken);

    Task<bool> HasAcceptedSubmissionAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Saves pending changes; returns false when a concurrency conflict was detected.</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
