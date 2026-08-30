using DNTU.SkillBridge.Application.Submissions;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the technical review data operations.</summary>
public sealed class TechnicalReviewRepository(AppDbContext dbContext) : ITechnicalReviewRepository
{
    /// <summary>Reads a submission with its versions with the context's default tracking behavior.</summary>
    public Task<ProjectSubmission?> FindSubmissionWithVersionsAsync(Guid submissionId, CancellationToken cancellationToken) =>
        dbContext.ProjectSubmissions
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == submissionId, cancellationToken);

    public Task<bool> IsAssignedActiveLecturerAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments.AsNoTracking()
            .Join(dbContext.LecturerProfiles.AsNoTracking(),
                assignment => assignment.LecturerId,
                lecturer => lecturer.Id,
                (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId &&
                             row.assignment.Status == LecturerAssignmentStatus.ACTIVE &&
                             row.lecturer.IsActive &&
                             row.lecturer.UserId == userId,
                cancellationToken);

    public void AddReview(TechnicalSubmissionReview review) => dbContext.TechnicalSubmissionReviews.Add(review);

    public void AddStatusHistory(SubmissionStatusHistory history) => dbContext.SubmissionStatusHistories.Add(history);

    public void AddOutboxMessage(OutboxMessage message) => dbContext.OutboxMessages.Add(message);

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

    public Task<Guid?> FindProjectIdBySubmissionIdAsync(Guid submissionId, CancellationToken cancellationToken) =>
        dbContext.ProjectSubmissions.AsNoTracking()
            .Where(submission => submission.Id == submissionId)
            .Select(submission => (Guid?)submission.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsProjectStudentAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AsNoTracking().AnyAsync(member =>
            member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId,
            cancellationToken);

    public async Task<IReadOnlyCollection<TechnicalReviewResponse>> ListForSubmissionAsync(Guid submissionId, CancellationToken cancellationToken) =>
        await dbContext.TechnicalSubmissionReviews.AsNoTracking()
            .Where(review => review.SubmissionId == submissionId)
            .OrderBy(review => review.CreatedAt).ThenBy(review => review.Id)
            .Select(review => Map(review))
            .ToListAsync(cancellationToken);

    public Task<TechnicalReviewLookupRow?> FindReviewRowAsync(Guid reviewId, CancellationToken cancellationToken) =>
        dbContext.TechnicalSubmissionReviews.AsNoTracking()
            .Where(item => item.Id == reviewId)
            .Select(item => new TechnicalReviewLookupRow(Map(item), item.SubmissionId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TechnicalReviewResponse>> ListMineAsync(Guid lecturerUserId, CancellationToken cancellationToken) =>
        await dbContext.TechnicalSubmissionReviews.AsNoTracking()
            .Where(review => review.ReviewerUserId == lecturerUserId)
            .OrderByDescending(review => review.CreatedAt).ThenByDescending(review => review.Id)
            .Select(review => Map(review))
            .ToListAsync(cancellationToken);

    private static TechnicalReviewResponse Map(TechnicalSubmissionReview review) => new(
        review.Id,
        review.SubmissionId,
        review.SubmissionVersionId,
        review.ReviewerUserId,
        review.Decision,
        review.Feedback,
        review.CriteriaNotes,
        review.CreatedAt);
}
