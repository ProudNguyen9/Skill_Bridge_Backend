using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.Application.Submissions;

/// <summary>Data operations for project submissions, evidence versions, and status history.</summary>
public interface ISubmissionRepository
{
    Task<Guid?> FindStudentIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> IsStudentMemberAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> IsValidMilestoneAsync(Guid projectId, Guid? milestoneId, CancellationToken cancellationToken);

    Task<bool> IsAuthorizedCompletedFileAsync(Guid userId, Guid projectId, Guid? fileId, CancellationToken cancellationToken);

    void AddSubmission(ProjectSubmission submission);
    void AddVersion(SubmissionVersion version);

    void AddStatusHistory(SubmissionStatusHistory history);

    /// <summary>Reads a submission with its versions with the context's default tracking behavior.</summary>
    Task<ProjectSubmission?> FindSubmissionWithVersionsAsync(Guid submissionId, CancellationToken cancellationToken);

    /// <summary>Reads a submission without tracking for read paths.</summary>
    Task<ProjectSubmission?> FindSubmissionAsync(Guid submissionId, CancellationToken cancellationToken);

    /// <summary>Persists pending changes; returns false when the update fails (conflict or persistence error).</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);

    Task<bool> CanReadAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionVersionResponse>> ListVersionsAsync(Guid submissionId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionStatusHistoryResponse>> ListHistoryAsync(Guid submissionId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionResponse>> ListStudentAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionResponse>> ListCompanyAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionResponse>> ListLecturerAsync(Guid userId, CancellationToken cancellationToken);
}
