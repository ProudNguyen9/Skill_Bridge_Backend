using System.Text.Json;
using DNTU.SkillBridge.Api.Workspaces;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Submissions;

public enum BusinessReviewOutcome
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    Invalid
}

/// <summary>Records immutable company business decisions after independent lecturer technical approval.</summary>
public sealed class BusinessReviewService(AppDbContext dbContext, IProjectActivityWriter activityWriter)
{
    public async Task<(BusinessReviewOutcome Outcome, BusinessReviewResponse? Review)> CreateAsync(
        Guid reviewerUserId,
        Guid submissionId,
        CreateBusinessReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Version == Guid.Empty || !Enum.IsDefined(request.Decision))
        {
            return (BusinessReviewOutcome.Invalid, null);
        }

        var submission = await dbContext.ProjectSubmissions
            .AsTracking()
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == submissionId, cancellationToken);
        if (submission is null)
        {
            return (BusinessReviewOutcome.NotFound, null);
        }

        var company = await dbContext.Projects.AsNoTracking()
            .Where(project => project.Id == submission.ProjectId)
            .Select(project => new { project.CompanyId, project.Company.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            return (BusinessReviewOutcome.NotFound, null);
        }

        if (!company.IsActive || !await CanReviewForCompanyAsync(reviewerUserId, company.CompanyId, cancellationToken))
        {
            return (BusinessReviewOutcome.Forbidden, null);
        }

        if (submission.Status != SubmissionStatus.TECHNICAL_APPROVED ||
            submission.Version != request.Version ||
            submission.Versions.SingleOrDefault(version => version.VersionNumber == submission.CurrentVersionNumber) is not { } currentVersion)
        {
            return (BusinessReviewOutcome.Conflict, null);
        }

        BusinessSubmissionReview review;
        try
        {
            review = new BusinessSubmissionReview(
                submission.Id,
                currentVersion.Id,
                company.CompanyId,
                reviewerUserId,
                request.Decision,
                request.RequirementsFeedback,
                request.CollaborationFeedback ?? string.Empty);

            var priorStatus = submission.Status;
            if (request.Decision == BusinessReviewDecision.ACCEPTED)
            {
                submission.AcceptBusiness(request.Version);
            }
            else
            {
                submission.RequireRevision(request.Version);
            }

            dbContext.BusinessSubmissionReviews.Add(review);
            dbContext.SubmissionStatusHistories.Add(new SubmissionStatusHistory(
                submission.Id,
                priorStatus,
                submission.Status,
                reviewerUserId));
        }
        catch (ArgumentException)
        {
            return (BusinessReviewOutcome.Invalid, null);
        }
        catch (InvalidOperationException)
        {
            return (BusinessReviewOutcome.Conflict, null);
        }

        var eventType = request.Decision == BusinessReviewDecision.ACCEPTED
            ? "SUBMISSION_BUSINESS_ACCEPTED"
            : "SUBMISSION_BUSINESS_REVISION_REQUIRED";
        var payload = JsonSerializer.Serialize(new
        {
            submissionId = submission.Id,
            submissionVersionId = currentVersion.Id,
            reviewId = review.Id,
            companyId = company.CompanyId,
            decision = request.Decision.ToString(),
            reviewerUserId
        });
        activityWriter.Append(submission.ProjectId, eventType, reviewerUserId, new
        {
            submissionId = submission.Id,
            submissionVersionId = currentVersion.Id,
            reviewId = review.Id,
            companyId = company.CompanyId,
            decision = request.Decision.ToString()
        });
        dbContext.OutboxMessages.Add(new OutboxMessage("submission.business-review.completed", payload));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (BusinessReviewOutcome.Conflict, null);
        }

        return (BusinessReviewOutcome.Success, Map(review));
    }

    public async Task<(BusinessReviewOutcome Outcome, IReadOnlyCollection<BusinessReviewResponse>? Reviews)> ListForSubmissionAsync(
        Guid requesterUserId,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var scope = await dbContext.ProjectSubmissions.AsNoTracking()
            .Where(submission => submission.Id == submissionId)
            .Join(dbContext.Projects.AsNoTracking(), submission => submission.ProjectId, project => project.Id,
                (submission, project) => new { submission.ProjectId, project.CompanyId })
            .SingleOrDefaultAsync(cancellationToken);
        if (scope is null)
        {
            return (BusinessReviewOutcome.NotFound, null);
        }

        if (!await CanReadBusinessReviewAsync(requesterUserId, scope.ProjectId, scope.CompanyId, cancellationToken))
        {
            return (BusinessReviewOutcome.Forbidden, null);
        }

        var reviews = await dbContext.BusinessSubmissionReviews.AsNoTracking()
            .Where(review => review.SubmissionId == submissionId)
            .OrderBy(review => review.CreatedAt).ThenBy(review => review.Id)
            .Select(review => Map(review))
            .ToListAsync(cancellationToken);
        return (BusinessReviewOutcome.Success, reviews);
    }

    public async Task<IReadOnlyCollection<BusinessReviewResponse>> ListMineAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.BusinessSubmissionReviews.AsNoTracking()
            .Where(review => review.ReviewerUserId == userId)
            .OrderByDescending(review => review.CreatedAt).ThenByDescending(review => review.Id)
            .Select(review => Map(review))
            .ToListAsync(cancellationToken);

    private Task<bool> CanReviewForCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken) =>
        dbContext.CompanyMembers.AsNoTracking().AnyAsync(member =>
            member.CompanyId == companyId &&
            member.UserId == userId &&
            (member.Role == CompanyMemberRole.OWNER || member.Role == CompanyMemberRole.MANAGER),
            cancellationToken);

    private async Task<bool> CanReadBusinessReviewAsync(Guid userId, Guid projectId, Guid companyId, CancellationToken cancellationToken) =>
        await dbContext.CompanyMembers.AsNoTracking().AnyAsync(member => member.CompanyId == companyId && member.UserId == userId, cancellationToken) ||
        await dbContext.ProjectMembers.AsNoTracking().AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken) ||
        await dbContext.LecturerAssignments.AsNoTracking()
            .Join(dbContext.LecturerProfiles.AsNoTracking(), assignment => assignment.LecturerId, lecturer => lecturer.Id,
                (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == Domain.Lecturers.LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId, cancellationToken);

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
