using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Files;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Submissions;

public enum SubmissionOutcome
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    Invalid
}

/// <summary>Creates immutable evidence snapshots and provides role-scoped submission projections.</summary>
public sealed class SubmissionService(AppDbContext dbContext, IProjectActivityWriter activityWriter)
{
    public async Task<(SubmissionOutcome Outcome, SubmissionResponse? Submission)> CreateAsync(Guid userId, Guid projectId, CreateSubmissionRequest request, CancellationToken cancellationToken)
    {
        var studentId = await StudentIdAsync(userId, cancellationToken);
        if (studentId is null || !await IsStudentMemberAsync(userId, projectId, cancellationToken)) return (SubmissionOutcome.Forbidden, null);
        if (!await IsValidMilestoneAsync(projectId, request.MilestoneId, cancellationToken) ||
            !await IsAuthorizedCompletedFileAsync(userId, projectId, request.FileId, cancellationToken)) return (SubmissionOutcome.Invalid, null);

        try
        {
            var version = new SubmissionVersion(request.Summary, request.GithubUrl, request.DemoUrl, request.VideoUrl, request.FileId);
            var submission = new ProjectSubmission(projectId, request.MilestoneId, studentId.Value, version);
            dbContext.ProjectSubmissions.Add(submission);
            dbContext.SubmissionStatusHistories.Add(new SubmissionStatusHistory(submission.Id, SubmissionStatus.SUBMITTED, SubmissionStatus.SUBMITTED, userId));
            AddEvent(projectId, "SUBMISSION_CREATED", userId, submission.Id, 1);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (SubmissionOutcome.Success, Map(submission));
        }
        catch (ArgumentException)
        {
            return (SubmissionOutcome.Invalid, null);
        }
    }

    public async Task<(SubmissionOutcome Outcome, SubmissionResponse? Submission)> NewVersionAsync(Guid userId, Guid submissionId, CreateSubmissionVersionRequest request, CancellationToken cancellationToken)
    {
        var submission = await dbContext.ProjectSubmissions.Include(item => item.Versions).SingleOrDefaultAsync(item => item.Id == submissionId, cancellationToken);
        if (submission is null) return (SubmissionOutcome.NotFound, null);
        if (!await IsStudentMemberAsync(userId, submission.ProjectId, cancellationToken)) return (SubmissionOutcome.Forbidden, null);
        if (!await IsAuthorizedCompletedFileAsync(userId, submission.ProjectId, request.FileId, cancellationToken)) return (SubmissionOutcome.Invalid, null);

        var previousStatus = submission.Status;
        try
        {
            submission.AddVersion(new SubmissionVersion(request.Summary, request.GithubUrl, request.DemoUrl, request.VideoUrl, request.FileId), request.Version);
        }
        catch (InvalidOperationException)
        {
            return (SubmissionOutcome.Conflict, null);
        }
        catch (ArgumentException)
        {
            return (SubmissionOutcome.Invalid, null);
        }

        dbContext.SubmissionStatusHistories.Add(new SubmissionStatusHistory(submission.Id, previousStatus, submission.Status, userId));
        AddEvent(submission.ProjectId, "SUBMISSION_NEW_VERSION", userId, submission.Id, submission.CurrentVersionNumber);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (SubmissionOutcome.Conflict, null);
        }
        catch (DbUpdateException)
        {
            return (SubmissionOutcome.Conflict, null);
        }

        return (SubmissionOutcome.Success, Map(submission));
    }

    public async Task<(SubmissionOutcome Outcome, SubmissionResponse? Submission)> GetAsync(Guid userId, Guid submissionId, CancellationToken cancellationToken)
    {
        var submission = await dbContext.ProjectSubmissions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == submissionId, cancellationToken);
        if (submission is null) return (SubmissionOutcome.NotFound, null);
        return await CanReadAsync(userId, submission.ProjectId, cancellationToken)
            ? (SubmissionOutcome.Success, Map(submission))
            : (SubmissionOutcome.Forbidden, null);
    }

    public async Task<(SubmissionOutcome Outcome, IReadOnlyCollection<SubmissionVersionResponse>? Versions)> ListVersionsAsync(Guid userId, Guid submissionId, CancellationToken cancellationToken)
    {
        var (outcome, _) = await GetAsync(userId, submissionId, cancellationToken);
        if (outcome != SubmissionOutcome.Success) return (outcome, null);
        var versions = await dbContext.SubmissionVersions.AsNoTracking().Where(item => item.SubmissionId == submissionId).OrderBy(item => item.VersionNumber)
            .Select(item => new SubmissionVersionResponse(item.Id, item.SubmissionId, item.VersionNumber, item.Summary, item.GithubUrl, item.DemoUrl, item.VideoUrl, item.FileId, item.CreatedAt)).ToListAsync(cancellationToken);
        return (SubmissionOutcome.Success, versions);
    }

    public async Task<(SubmissionOutcome Outcome, IReadOnlyCollection<SubmissionStatusHistoryResponse>? History)> ListHistoryAsync(Guid userId, Guid submissionId, CancellationToken cancellationToken)
    {
        var (outcome, _) = await GetAsync(userId, submissionId, cancellationToken);
        if (outcome != SubmissionOutcome.Success) return (outcome, null);
        var history = await dbContext.SubmissionStatusHistories.AsNoTracking().Where(item => item.SubmissionId == submissionId).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new SubmissionStatusHistoryResponse(item.Id, item.FromStatus.ToString(), item.ToStatus.ToString(), item.ActorUserId, item.CreatedAt)).ToListAsync(cancellationToken);
        return (SubmissionOutcome.Success, history);
    }

    public async Task<IReadOnlyCollection<SubmissionResponse>> ListStudentAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.ProjectSubmissions.AsNoTracking()
            .Where(item => dbContext.ProjectMembers.Any(member => member.ProjectId == item.ProjectId && member.IsActive && member.Student.UserId == userId))
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => new SubmissionResponse(item.Id, item.ProjectId, item.MilestoneId, item.Status.ToString(), item.CurrentVersionNumber, item.SubmittedByStudentId, item.Version, item.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<SubmissionResponse>> ListCompanyAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.ProjectSubmissions.AsNoTracking()
            .Where(item => dbContext.Projects.Any(project => project.Id == item.ProjectId && project.Company.Members.Any(member => member.UserId == userId)))
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => new SubmissionResponse(item.Id, item.ProjectId, item.MilestoneId, item.Status.ToString(), item.CurrentVersionNumber, item.SubmittedByStudentId, item.Version, item.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<SubmissionResponse>> ListLecturerAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.ProjectSubmissions.AsNoTracking()
            .Where(item => dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles,
                assignment => assignment.LecturerId,
                lecturer => lecturer.Id,
                (assignment, lecturer) => new { assignment, lecturer })
                .Any(row => row.assignment.ProjectId == item.ProjectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId))
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => new SubmissionResponse(item.Id, item.ProjectId, item.MilestoneId, item.Status.ToString(), item.CurrentVersionNumber, item.SubmittedByStudentId, item.Version, item.CreatedAt))
            .ToListAsync(cancellationToken);

    private Task<Guid?> StudentIdAsync(Guid userId, CancellationToken cancellationToken) => dbContext.StudentProfiles.AsNoTracking().Where(profile => profile.UserId == userId).Select(profile => (Guid?)profile.Id).SingleOrDefaultAsync(cancellationToken);
    private Task<bool> IsStudentMemberAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) => dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken);
    private async Task<bool> IsValidMilestoneAsync(Guid projectId, Guid? milestoneId, CancellationToken cancellationToken) =>
        !milestoneId.HasValue || await dbContext.ProjectMilestones.AnyAsync(item => item.Id == milestoneId.Value && item.ProjectId == projectId && item.Status != DNTU.SkillBridge.Domain.Milestones.MilestoneStatus.APPROVED, cancellationToken);
    private async Task<bool> IsAuthorizedCompletedFileAsync(Guid userId, Guid projectId, Guid? fileId, CancellationToken cancellationToken) =>
        !fileId.HasValue || await dbContext.FileRecords.AnyAsync(file => file.Id == fileId.Value && file.ProjectId == projectId && file.UploadedByUserId == userId && file.Status == FileUploadStatus.COMPLETED, cancellationToken);

    private async Task<bool> CanReadAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await IsStudentMemberAsync(userId, projectId, cancellationToken) ||
        await dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member => member.UserId == userId), cancellationToken) ||
        await dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles, assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId, cancellationToken);

    private void AddEvent(Guid projectId, string eventType, Guid userId, Guid submissionId, int versionNumber) =>
        activityWriter.Append(projectId, eventType, userId, new { submissionId, versionNumber });
    private static SubmissionResponse Map(ProjectSubmission submission) => new(submission.Id, submission.ProjectId, submission.MilestoneId, submission.Status.ToString(), submission.CurrentVersionNumber, submission.SubmittedByStudentId, submission.Version, submission.CreatedAt);
}
