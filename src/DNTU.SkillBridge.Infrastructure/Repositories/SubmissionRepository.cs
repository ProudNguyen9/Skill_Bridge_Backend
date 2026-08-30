using DNTU.SkillBridge.Application.Submissions;
using DNTU.SkillBridge.Domain.Files;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Milestones;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the submission data operations.</summary>
public sealed class SubmissionRepository(AppDbContext dbContext) : ISubmissionRepository
{
    public Task<Guid?> FindStudentIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles.AsNoTracking().Where(profile => profile.UserId == userId).Select(profile => (Guid?)profile.Id).SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsStudentMemberAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken);

    public async Task<bool> IsValidMilestoneAsync(Guid projectId, Guid? milestoneId, CancellationToken cancellationToken) =>
        !milestoneId.HasValue || await dbContext.ProjectMilestones.AnyAsync(item => item.Id == milestoneId.Value && item.ProjectId == projectId && item.Status != MilestoneStatus.APPROVED, cancellationToken);

    public async Task<bool> IsAuthorizedCompletedFileAsync(Guid userId, Guid projectId, Guid? fileId, CancellationToken cancellationToken) =>
        !fileId.HasValue || await dbContext.FileRecords.AnyAsync(file => file.Id == fileId.Value && file.ProjectId == projectId && file.UploadedByUserId == userId && file.Status == FileUploadStatus.COMPLETED, cancellationToken);

    public void AddSubmission(ProjectSubmission submission) => dbContext.ProjectSubmissions.Add(submission);

    public void AddStatusHistory(SubmissionStatusHistory history) => dbContext.SubmissionStatusHistories.Add(history);

    /// <summary>Reads a submission with its versions with the context's default tracking behavior.</summary>
    public Task<ProjectSubmission?> FindSubmissionWithVersionsAsync(Guid submissionId, CancellationToken cancellationToken) =>
        dbContext.ProjectSubmissions.Include(item => item.Versions).SingleOrDefaultAsync(item => item.Id == submissionId, cancellationToken);

    /// <summary>Reads a submission without tracking for read paths.</summary>
    public Task<ProjectSubmission?> FindSubmissionAsync(Guid submissionId, CancellationToken cancellationToken) =>
        dbContext.ProjectSubmissions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == submissionId, cancellationToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    public async Task<bool> CanReadAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await IsStudentMemberAsync(userId, projectId, cancellationToken) ||
        await dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member => member.UserId == userId), cancellationToken) ||
        await dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles, assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId, cancellationToken);

    public async Task<IReadOnlyCollection<SubmissionVersionResponse>> ListVersionsAsync(Guid submissionId, CancellationToken cancellationToken) =>
        await dbContext.SubmissionVersions.AsNoTracking().Where(item => item.SubmissionId == submissionId).OrderBy(item => item.VersionNumber)
            .Select(item => new SubmissionVersionResponse(item.Id, item.SubmissionId, item.VersionNumber, item.Summary, item.GithubUrl, item.DemoUrl, item.VideoUrl, item.FileId, item.CreatedAt)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<SubmissionStatusHistoryResponse>> ListHistoryAsync(Guid submissionId, CancellationToken cancellationToken) =>
        await dbContext.SubmissionStatusHistories.AsNoTracking().Where(item => item.SubmissionId == submissionId).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new SubmissionStatusHistoryResponse(item.Id, item.FromStatus.ToString(), item.ToStatus.ToString(), item.ActorUserId, item.CreatedAt)).ToListAsync(cancellationToken);

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
}
