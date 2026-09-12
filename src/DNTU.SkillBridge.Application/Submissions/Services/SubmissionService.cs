using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.Application.Submissions;

/// <summary>Creates immutable evidence snapshots and provides role-scoped submission projections.</summary>
public sealed class SubmissionService(ISubmissionRepository submissionRepository, IProjectActivityWriter activityWriter, IUnitOfWork unitOfWork) : ISubmissionService
{
    public async Task<(SubmissionOutcome Outcome, SubmissionResponse? Submission)> CreateAsync(Guid userId, Guid projectId, CreateSubmissionRequest request, CancellationToken cancellationToken)
    {
        var studentId = await submissionRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null || !await submissionRepository.IsStudentMemberAsync(userId, projectId, cancellationToken)) return (SubmissionOutcome.Forbidden, null);
        if (!await submissionRepository.IsValidMilestoneAsync(projectId, request.MilestoneId, cancellationToken) ||
            !await submissionRepository.IsAuthorizedCompletedFileAsync(userId, projectId, request.FileId, cancellationToken)) return (SubmissionOutcome.Invalid, null);

        try
        {
            var version = new SubmissionVersion(request.Summary, request.GithubUrl, request.DemoUrl, request.VideoUrl, request.FileId);
            var submission = new ProjectSubmission(projectId, request.MilestoneId, studentId.Value, version);
            submissionRepository.AddSubmission(submission);
            submissionRepository.AddStatusHistory(new SubmissionStatusHistory(submission.Id, SubmissionStatus.SUBMITTED, SubmissionStatus.SUBMITTED, userId));
            AddEvent(projectId, "SUBMISSION_CREATED", userId, submission.Id, 1);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return (SubmissionOutcome.Success, Map(submission));
        }
        catch (ArgumentException)
        {
            return (SubmissionOutcome.Invalid, null);
        }
    }

    public async Task<(SubmissionOutcome Outcome, SubmissionResponse? Submission)> NewVersionAsync(Guid userId, Guid submissionId, CreateSubmissionVersionRequest request, CancellationToken cancellationToken)
    {
        var submission = await submissionRepository.FindSubmissionWithVersionsAsync(submissionId, cancellationToken);
        if (submission is null) return (SubmissionOutcome.NotFound, null);
        if (!await submissionRepository.IsStudentMemberAsync(userId, submission.ProjectId, cancellationToken)) return (SubmissionOutcome.Forbidden, null);
        if (!await submissionRepository.IsAuthorizedCompletedFileAsync(userId, submission.ProjectId, request.FileId, cancellationToken)) return (SubmissionOutcome.Invalid, null);

        var previousStatus = submission.Status;
        try
        {
            var version = new SubmissionVersion(request.Summary, request.GithubUrl, request.DemoUrl, request.VideoUrl, request.FileId);
            submission.AddVersion(version, request.Version);
            submissionRepository.AddVersion(version);
        }
        catch (InvalidOperationException)
        {
            return (SubmissionOutcome.Conflict, null);
        }
        catch (ArgumentException)
        {
            return (SubmissionOutcome.Invalid, null);
        }

        submissionRepository.AddStatusHistory(new SubmissionStatusHistory(submission.Id, previousStatus, submission.Status, userId));
        AddEvent(submission.ProjectId, "SUBMISSION_NEW_VERSION", userId, submission.Id, submission.CurrentVersionNumber);
        if (!await submissionRepository.TrySaveChangesAsync(cancellationToken)) return (SubmissionOutcome.Conflict, null);

        return (SubmissionOutcome.Success, Map(submission));
    }

    public async Task<(SubmissionOutcome Outcome, SubmissionResponse? Submission)> GetAsync(Guid userId, Guid submissionId, CancellationToken cancellationToken)
    {
        var submission = await submissionRepository.FindSubmissionAsync(submissionId, cancellationToken);
        if (submission is null) return (SubmissionOutcome.NotFound, null);
        return await submissionRepository.CanReadAsync(userId, submission.ProjectId, cancellationToken)
            ? (SubmissionOutcome.Success, Map(submission))
            : (SubmissionOutcome.Forbidden, null);
    }

    public async Task<(SubmissionOutcome Outcome, IReadOnlyCollection<SubmissionVersionResponse>? Versions)> ListVersionsAsync(Guid userId, Guid submissionId, CancellationToken cancellationToken)
    {
        var (outcome, _) = await GetAsync(userId, submissionId, cancellationToken);
        if (outcome != SubmissionOutcome.Success) return (outcome, null);
        var versions = await submissionRepository.ListVersionsAsync(submissionId, cancellationToken);
        return (SubmissionOutcome.Success, versions);
    }

    public async Task<(SubmissionOutcome Outcome, IReadOnlyCollection<SubmissionStatusHistoryResponse>? History)> ListHistoryAsync(Guid userId, Guid submissionId, CancellationToken cancellationToken)
    {
        var (outcome, _) = await GetAsync(userId, submissionId, cancellationToken);
        if (outcome != SubmissionOutcome.Success) return (outcome, null);
        var history = await submissionRepository.ListHistoryAsync(submissionId, cancellationToken);
        return (SubmissionOutcome.Success, history);
    }

    public Task<IReadOnlyCollection<SubmissionResponse>> ListStudentAsync(Guid userId, CancellationToken cancellationToken) =>
        submissionRepository.ListStudentAsync(userId, cancellationToken);

    public Task<IReadOnlyCollection<SubmissionResponse>> ListCompanyAsync(Guid userId, CancellationToken cancellationToken) =>
        submissionRepository.ListCompanyAsync(userId, cancellationToken);

    public Task<IReadOnlyCollection<SubmissionResponse>> ListLecturerAsync(Guid userId, CancellationToken cancellationToken) =>
        submissionRepository.ListLecturerAsync(userId, cancellationToken);

    private void AddEvent(Guid projectId, string eventType, Guid userId, Guid submissionId, int versionNumber) =>
        activityWriter.Append(projectId, eventType, userId, new { submissionId, versionNumber });
    private static SubmissionResponse Map(ProjectSubmission submission) => new(submission.Id, submission.ProjectId, submission.MilestoneId, submission.Status.ToString(), submission.CurrentVersionNumber, submission.SubmittedByStudentId, submission.Version, submission.CreatedAt);
}
