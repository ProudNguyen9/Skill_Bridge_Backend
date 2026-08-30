using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.Application.Submissions;

public interface ISubmissionService
{
    Task<(SubmissionOutcome Outcome, SubmissionResponse? Submission)> CreateAsync(Guid userId, Guid projectId, CreateSubmissionRequest request, CancellationToken cancellationToken);

    Task<(SubmissionOutcome Outcome, SubmissionResponse? Submission)> NewVersionAsync(Guid userId, Guid submissionId, CreateSubmissionVersionRequest request, CancellationToken cancellationToken);

    Task<(SubmissionOutcome Outcome, SubmissionResponse? Submission)> GetAsync(Guid userId, Guid submissionId, CancellationToken cancellationToken);

    Task<(SubmissionOutcome Outcome, IReadOnlyCollection<SubmissionVersionResponse>? Versions)> ListVersionsAsync(Guid userId, Guid submissionId, CancellationToken cancellationToken);

    Task<(SubmissionOutcome Outcome, IReadOnlyCollection<SubmissionStatusHistoryResponse>? History)> ListHistoryAsync(Guid userId, Guid submissionId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionResponse>> ListStudentAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionResponse>> ListCompanyAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionResponse>> ListLecturerAsync(Guid userId, CancellationToken cancellationToken);
}
