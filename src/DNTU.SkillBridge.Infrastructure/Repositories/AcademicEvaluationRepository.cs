using DNTU.SkillBridge.Application.Academics;
using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the academic evaluation data operations.</summary>
public sealed class AcademicEvaluationRepository(AppDbContext dbContext) : IAcademicEvaluationRepository
{
    public async Task<IReadOnlyCollection<AcademicEvaluation>> ListForLecturerAsync(Guid lecturerUserId, CancellationToken cancellationToken) =>
        await dbContext.AcademicEvaluations.AsNoTracking()
            .Where(item => item.EvaluatorUserId == lecturerUserId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<AcademicEvaluation?> FindEvaluationAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.AcademicEvaluations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.StudentId == studentId, cancellationToken);

    public Task<AcademicEvaluation?> FindEvaluationWithScoresAsync(Guid projectId, Guid studentId, Guid rubricId, CancellationToken cancellationToken) =>
        dbContext.AcademicEvaluations
            .Include(item => item.Scores)
            .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.StudentId == studentId && item.RubricId == rubricId, cancellationToken);

    public Task<AcademicEvaluation?> FindEvaluationForUpdateAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.AcademicEvaluations.SingleOrDefaultAsync(item => item.ProjectId == projectId && item.StudentId == studentId, cancellationToken);

    public void AddEvaluation(AcademicEvaluation evaluation) => dbContext.AcademicEvaluations.Add(evaluation);

    public Task<bool> IsAssignedActiveLecturerAsync(Guid lecturerUserId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments
            .Join(dbContext.LecturerProfiles,
                assignment => assignment.LecturerId,
                profile => profile.Id,
                (assignment, profile) => new { assignment, profile })
            .AnyAsync(row => row.assignment.ProjectId == projectId &&
                             row.assignment.Status == LecturerAssignmentStatus.ACTIVE &&
                             row.profile.IsActive &&
                             row.profile.UserId == lecturerUserId,
                cancellationToken);

    public Task<bool> CanLecturerReadProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments.AsNoTracking()
            .Join(dbContext.LecturerProfiles.AsNoTracking(), assignment => assignment.LecturerId, profile => profile.Id, (assignment, profile) => new { assignment, profile })
            .AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.profile.UserId == userId && row.profile.IsActive, cancellationToken);

    public Task<bool> HasActiveProjectMemberAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.StudentId == studentId && member.IsActive, cancellationToken);

    public Task<bool> HasActiveMemberForUserAsync(Guid projectId, Guid studentId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AsNoTracking().AnyAsync(member => member.ProjectId == projectId && member.StudentId == studentId && member.IsActive && member.Student.UserId == userId, cancellationToken);

    public Task<Rubric?> FindLockedRubricWithCriteriaAsync(Guid rubricId, CancellationToken cancellationToken) =>
        dbContext.Rubrics.Include(item => item.Criteria)
            .SingleOrDefaultAsync(item => item.Id == rubricId && item.IsLocked, cancellationToken);

    public Task<bool> HasApprovedCourseProjectAsync(Guid projectId, Guid rubricId, CancellationToken cancellationToken) =>
        dbContext.CourseProjects.AnyAsync(mapping =>
            mapping.ProjectId == projectId &&
            mapping.RubricId == rubricId &&
            mapping.Status == CourseProjectStatus.APPROVED,
            cancellationToken);

    public Task<bool> HasAcceptedSubmissionAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectSubmissions.AsNoTracking()
            .AnyAsync(submission => submission.ProjectId == projectId && submission.Status == Domain.Submissions.SubmissionStatus.BUSINESS_ACCEPTED, cancellationToken);

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
    }
}
