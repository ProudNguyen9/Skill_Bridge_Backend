using DNTU.SkillBridge.Application.Submissions;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the business review data operations.</summary>
public sealed class BusinessReviewRepository(AppDbContext dbContext) : IBusinessReviewRepository
{
    /// <summary>Reads a submission with its versions as a tracked entity so mutations are persisted.</summary>
    public Task<ProjectSubmission?> FindSubmissionForUpdateAsync(Guid submissionId, CancellationToken cancellationToken) =>
        dbContext.ProjectSubmissions
            .AsTracking()
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == submissionId, cancellationToken);

    public async Task<(Guid CompanyId, bool CompanyIsActive)?> FindProjectCompanyAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Projects.AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new { project.CompanyId, project.Company.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        return company is null ? null : (company.CompanyId, company.IsActive);
    }

    public Task<bool> CanReviewForCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken) =>
        dbContext.CompanyMembers.AsNoTracking().AnyAsync(member =>
            member.CompanyId == companyId &&
            member.UserId == userId &&
            (member.Role == CompanyMemberRole.OWNER || member.Role == CompanyMemberRole.MANAGER),
            cancellationToken);

    public void AddReview(BusinessSubmissionReview review) => dbContext.BusinessSubmissionReviews.Add(review);

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
    }

    public async Task<(Guid ProjectId, Guid CompanyId)?> FindSubmissionScopeAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var scope = await dbContext.ProjectSubmissions.AsNoTracking()
            .Where(submission => submission.Id == submissionId)
            .Join(dbContext.Projects.AsNoTracking(), submission => submission.ProjectId, project => project.Id,
                (submission, project) => new { submission.ProjectId, project.CompanyId })
            .SingleOrDefaultAsync(cancellationToken);
        return scope is null ? null : (scope.ProjectId, scope.CompanyId);
    }

    public async Task<bool> CanReadBusinessReviewAsync(Guid userId, Guid projectId, Guid companyId, CancellationToken cancellationToken) =>
        await dbContext.CompanyMembers.AsNoTracking().AnyAsync(member => member.CompanyId == companyId && member.UserId == userId, cancellationToken) ||
        await dbContext.ProjectMembers.AsNoTracking().AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken) ||
        await dbContext.LecturerAssignments.AsNoTracking()
            .Join(dbContext.LecturerProfiles.AsNoTracking(), assignment => assignment.LecturerId, lecturer => lecturer.Id,
                (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId, cancellationToken);

    public async Task<IReadOnlyCollection<BusinessReviewResponse>> ListForSubmissionAsync(Guid submissionId, CancellationToken cancellationToken) =>
        await dbContext.BusinessSubmissionReviews.AsNoTracking()
            .Where(review => review.SubmissionId == submissionId)
            .OrderBy(review => review.CreatedAt).ThenBy(review => review.Id)
            .Select(review => Map(review))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<BusinessReviewResponse>> ListMineAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.BusinessSubmissionReviews.AsNoTracking()
            .Where(review => review.ReviewerUserId == userId)
            .OrderByDescending(review => review.CreatedAt).ThenByDescending(review => review.Id)
            .Select(review => Map(review))
            .ToListAsync(cancellationToken);

    private static BusinessReviewResponse Map(BusinessSubmissionReview review) => new(
        review.Id,
        review.SubmissionId,
        review.SubmissionVersionId,
        review.CompanyId,
        review.ReviewerUserId,
        review.Decision,
        review.RequirementsFeedback,
        review.CollaborationFeedback,
        review.CreatedAt);
}
