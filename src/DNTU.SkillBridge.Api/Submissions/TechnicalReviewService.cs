using System.Text.Json;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Submissions;

public enum TechnicalReviewOutcome
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    Invalid
}

/// <summary>Creates immutable technical review history and atomically advances the submission workflow.</summary>
public sealed class TechnicalReviewService(AppDbContext dbContext, IProjectActivityWriter activityWriter)
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

        var submission = await dbContext.ProjectSubmissions
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == submissionId, cancellationToken);
        if (submission is null)
        {
            return (TechnicalReviewOutcome.NotFound, null);
        }

        if (submission.Status is not (SubmissionStatus.SUBMITTED or SubmissionStatus.RESUBMITTED))
        {
            return (TechnicalReviewOutcome.Conflict, null);
        }

        if (!mayOverrideLecturerScope && !await IsAssignedActiveLecturerAsync(reviewerUserId, submission.ProjectId, cancellationToken))
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

            dbContext.TechnicalSubmissionReviews.Add(review);
            dbContext.SubmissionStatusHistories.Add(new SubmissionStatusHistory(
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
        dbContext.OutboxMessages.Add(new OutboxMessage("submission.technical-review.completed", payload));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (TechnicalReviewOutcome.Conflict, null);
        }
        catch (DbUpdateException)
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
        var projectId = await dbContext.ProjectSubmissions.AsNoTracking()
            .Where(submission => submission.Id == submissionId)
            .Select(submission => (Guid?)submission.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);
        if (projectId is null)
        {
            return (TechnicalReviewOutcome.NotFound, null);
        }

        if (!mayOverrideLecturerScope &&
            !await IsProjectStudentAsync(requesterUserId, projectId.Value, cancellationToken) &&
            !await IsAssignedActiveLecturerAsync(requesterUserId, projectId.Value, cancellationToken))
        {
            return (TechnicalReviewOutcome.Forbidden, null);
        }

        var reviews = await dbContext.TechnicalSubmissionReviews.AsNoTracking()
            .Where(review => review.SubmissionId == submissionId)
            .OrderBy(review => review.CreatedAt).ThenBy(review => review.Id)
            .Select(review => Map(review))
            .ToListAsync(cancellationToken);
        return (TechnicalReviewOutcome.Success, reviews);
    }

    public async Task<(TechnicalReviewOutcome Outcome, TechnicalReviewResponse? Review)> GetAsync(
        Guid requesterUserId,
        bool mayOverrideLecturerScope,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var review = await dbContext.TechnicalSubmissionReviews.AsNoTracking()
            .Where(item => item.Id == reviewId)
            .Select(item => new { Response = Map(item), item.SubmissionId })
            .SingleOrDefaultAsync(cancellationToken);
        if (review is null)
        {
            return (TechnicalReviewOutcome.NotFound, null);
        }

        var (outcome, _) = await ListForSubmissionAsync(requesterUserId, mayOverrideLecturerScope, review.SubmissionId, cancellationToken);
        return outcome == TechnicalReviewOutcome.Success
            ? (TechnicalReviewOutcome.Success, review.Response)
            : (outcome, null);
    }

    public async Task<IReadOnlyCollection<TechnicalReviewResponse>> ListMineAsync(Guid lecturerUserId, CancellationToken cancellationToken) =>
        await dbContext.TechnicalSubmissionReviews.AsNoTracking()
            .Where(review => review.ReviewerUserId == lecturerUserId)
            .OrderByDescending(review => review.CreatedAt).ThenByDescending(review => review.Id)
            .Select(review => Map(review))
            .ToListAsync(cancellationToken);

    private Task<bool> IsProjectStudentAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AsNoTracking().AnyAsync(member =>
            member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId,
            cancellationToken);

    private Task<bool> IsAssignedActiveLecturerAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
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
