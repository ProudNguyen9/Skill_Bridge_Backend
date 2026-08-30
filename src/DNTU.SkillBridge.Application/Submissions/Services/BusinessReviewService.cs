using System.Text.Json;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.Application.Submissions;

/// <summary>Records immutable company business decisions after independent lecturer technical approval.</summary>
public sealed class BusinessReviewService(IBusinessReviewRepository reviewRepository, IProjectActivityWriter activityWriter) : IBusinessReviewService
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

        var submission = await reviewRepository.FindSubmissionForUpdateAsync(submissionId, cancellationToken);
        if (submission is null)
        {
            return (BusinessReviewOutcome.NotFound, null);
        }

        var company = await reviewRepository.FindProjectCompanyAsync(submission.ProjectId, cancellationToken);
        if (company is null)
        {
            return (BusinessReviewOutcome.NotFound, null);
        }

        if (!company.Value.CompanyIsActive || !await reviewRepository.CanReviewForCompanyAsync(reviewerUserId, company.Value.CompanyId, cancellationToken))
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
                company.Value.CompanyId,
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

            reviewRepository.AddReview(review);
            reviewRepository.AddStatusHistory(new SubmissionStatusHistory(
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
            companyId = company.Value.CompanyId,
            decision = request.Decision.ToString(),
            reviewerUserId
        });
        activityWriter.Append(submission.ProjectId, eventType, reviewerUserId, new
        {
            submissionId = submission.Id,
            submissionVersionId = currentVersion.Id,
            reviewId = review.Id,
            companyId = company.Value.CompanyId,
            decision = request.Decision.ToString()
        });
        reviewRepository.AddOutboxMessage(new OutboxMessage("submission.business-review.completed", payload));

        if (!await reviewRepository.TrySaveChangesAsync(cancellationToken))
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
        var scope = await reviewRepository.FindSubmissionScopeAsync(submissionId, cancellationToken);
        if (scope is null)
        {
            return (BusinessReviewOutcome.NotFound, null);
        }

        if (!await reviewRepository.CanReadBusinessReviewAsync(requesterUserId, scope.Value.ProjectId, scope.Value.CompanyId, cancellationToken))
        {
            return (BusinessReviewOutcome.Forbidden, null);
        }

        var reviews = await reviewRepository.ListForSubmissionAsync(submissionId, cancellationToken);
        return (BusinessReviewOutcome.Success, reviews);
    }

    public Task<IReadOnlyCollection<BusinessReviewResponse>> ListMineAsync(Guid userId, CancellationToken cancellationToken) =>
        reviewRepository.ListMineAsync(userId, cancellationToken);

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
