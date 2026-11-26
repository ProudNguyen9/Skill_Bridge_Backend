using System.Text.Json;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.Application.Submissions;

/// <summary>Creates immutable technical review history and atomically advances the submission workflow.</summary>
public sealed class TechnicalReviewService(
    ITechnicalReviewRepository reviewRepository,
    IProjectActivityWriter activityWriter,
    INotificationWriter? notificationWriter = null) : ITechnicalReviewService
{
    public async Task<(TechnicalReviewOutcome Outcome, TechnicalReviewResponse? Review)> CreateAsync(
        Guid reviewerUserId,
        bool mayOverrideLecturerScope,
        Guid submissionId,
        CreateTechnicalReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Decision) || request.Version == Guid.Empty)
        {
            return (TechnicalReviewOutcome.Invalid, null);
        }

        var submission = await reviewRepository.FindSubmissionWithVersionsAsync(submissionId, cancellationToken);
        if (submission is null)
        {
            return (TechnicalReviewOutcome.NotFound, null);
        }

        if (submission.Status is not (SubmissionStatus.SUBMITTED or SubmissionStatus.RESUBMITTED))
        {
            return (TechnicalReviewOutcome.Conflict, null);
        }

        if (!mayOverrideLecturerScope && !await reviewRepository.IsAssignedActiveLecturerAsync(reviewerUserId, submission.ProjectId, cancellationToken))
        {
            return (TechnicalReviewOutcome.Forbidden, null);
        }

        var currentVersion = submission.Versions.SingleOrDefault(version => version.VersionNumber == submission.CurrentVersionNumber);
        if (currentVersion is null)
        {
            return (TechnicalReviewOutcome.Conflict, null);
        }

        TechnicalSubmissionReview review;
        try
        {
            review = new TechnicalSubmissionReview(
                submission.Id,
                currentVersion.Id,
                reviewerUserId,
                request.Decision,
                request.Feedback,
                request.CriteriaNotes);

            var priorStatus = submission.Status;
            if (request.Decision == TechnicalReviewDecision.APPROVED)
            {
                submission.ApproveTechnical(request.Version);
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
            return (TechnicalReviewOutcome.Invalid, null);
        }
        catch (InvalidOperationException)
        {
            return (TechnicalReviewOutcome.Conflict, null);
        }

        var eventType = request.Decision == TechnicalReviewDecision.APPROVED
            ? "SUBMISSION_TECHNICALLY_APPROVED"
            : "SUBMISSION_REVISION_REQUIRED";
        var payload = JsonSerializer.Serialize(new
        {
            submissionId = submission.Id,
            submissionVersionId = currentVersion.Id,
            reviewId = review.Id,
            decision = request.Decision.ToString(),
            reviewerUserId
        });
        activityWriter.Append(submission.ProjectId, eventType, reviewerUserId, new
        {
            submissionId = submission.Id,
            submissionVersionId = currentVersion.Id,
            reviewId = review.Id,
            decision = request.Decision.ToString()
        });
        reviewRepository.AddOutboxMessage(new OutboxMessage("submission.technical-review.completed", payload));

        if (notificationWriter is not null)
        {
            var scope = await reviewRepository.GetNotificationScopeAsync(submission.ProjectId, cancellationToken);
            if (scope is not null)
            {
                var approved = request.Decision == TechnicalReviewDecision.APPROVED;
                var notifType = approved ? "SUBMISSION_TECHNICAL_APPROVED" : "SUBMISSION_REVISION_REQUIRED";
                var studentTitle = approved
                    ? $"Bài nộp dự án “{scope.Value.ProjectTitle}” đã đạt thẩm định kỹ thuật của Giảng viên"
                    : $"Bài nộp dự án “{scope.Value.ProjectTitle}” cần chỉnh sửa theo góp ý của Giảng viên";
                var studentPayload = JsonSerializer.Serialize(new
                {
                    projectId = submission.ProjectId,
                    submissionId = submission.Id,
                    projectTitle = scope.Value.ProjectTitle,
                    feedback = request.Feedback,
                    url = $"/student/workspace/{submission.ProjectId}",
                });
                foreach (var studentUserId in scope.Value.StudentUserIds)
                {
                    notificationWriter.Add(new NotificationMessage(studentUserId, notifType, studentTitle, studentPayload));
                }

                if (approved)
                {
                    var companyPayload = JsonSerializer.Serialize(new
                    {
                        projectId = submission.ProjectId,
                        submissionId = submission.Id,
                        projectTitle = scope.Value.ProjectTitle,
                        url = $"/company/workspace/{submission.ProjectId}",
                    });
                    foreach (var companyUserId in scope.Value.CompanyUserIds)
                    {
                        notificationWriter.Add(new NotificationMessage(
                            companyUserId,
                            "SUBMISSION_TECHNICAL_APPROVED",
                            $"Bài nộp dự án “{scope.Value.ProjectTitle}” đã qua thẩm định kỹ thuật — chờ doanh nghiệp nghiệm thu",
                            companyPayload));
                    }
                }
            }
        }

        if (!await reviewRepository.TrySaveChangesAsync(cancellationToken))
        {
            return (TechnicalReviewOutcome.Conflict, null);
        }

        return (TechnicalReviewOutcome.Success, Map(review));
    }

    public async Task<(TechnicalReviewOutcome Outcome, IReadOnlyCollection<TechnicalReviewResponse>? Reviews)> ListForSubmissionAsync(
        Guid requesterUserId,
        bool mayOverrideLecturerScope,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var projectId = await reviewRepository.FindProjectIdBySubmissionIdAsync(submissionId, cancellationToken);
        if (projectId is null)
        {
            return (TechnicalReviewOutcome.NotFound, null);
        }

        if (!mayOverrideLecturerScope &&
            !await reviewRepository.IsProjectStudentAsync(requesterUserId, projectId.Value, cancellationToken) &&
            !await reviewRepository.IsAssignedActiveLecturerAsync(requesterUserId, projectId.Value, cancellationToken))
        {
            return (TechnicalReviewOutcome.Forbidden, null);
        }

        var reviews = await reviewRepository.ListForSubmissionAsync(submissionId, cancellationToken);
        return (TechnicalReviewOutcome.Success, reviews);
    }

    public async Task<(TechnicalReviewOutcome Outcome, TechnicalReviewResponse? Review)> GetAsync(
        Guid requesterUserId,
        bool mayOverrideLecturerScope,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var review = await reviewRepository.FindReviewRowAsync(reviewId, cancellationToken);
        if (review is null)
        {
            return (TechnicalReviewOutcome.NotFound, null);
        }

        var (outcome, _) = await ListForSubmissionAsync(requesterUserId, mayOverrideLecturerScope, review.SubmissionId, cancellationToken);
        return outcome == TechnicalReviewOutcome.Success
            ? (TechnicalReviewOutcome.Success, review.Response)
            : (outcome, null);
    }

    public Task<IReadOnlyCollection<TechnicalReviewResponse>> ListMineAsync(Guid lecturerUserId, CancellationToken cancellationToken) =>
        reviewRepository.ListMineAsync(lecturerUserId, cancellationToken);

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
