using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Submissions;

public enum TechnicalReviewDecision
{
    APPROVED = 1,
    REVISION_REQUIRED = 2
}

public sealed class TechnicalSubmissionReview : AuditableEntity
{
    public Guid SubmissionId { get; private set; }
    public Guid SubmissionVersionId { get; private set; }
    public Guid ReviewerUserId { get; private set; }
    public TechnicalReviewDecision Decision { get; private set; }
    public string Feedback { get; private set; } = string.Empty;

    /// <summary>Optional rubric-ready JSON/object notes captured as an immutable snapshot.</summary>
    public string? CriteriaNotes { get; private set; }

    private TechnicalSubmissionReview() { }

    public TechnicalSubmissionReview(
        Guid submissionId,
        Guid submissionVersionId,
        Guid reviewerUserId,
        TechnicalReviewDecision decision,
        string feedback,
        string? criteriaNotes)
    {
        if (submissionId == Guid.Empty) throw new ArgumentException("A submission is required.", nameof(submissionId));
        if (submissionVersionId == Guid.Empty) throw new ArgumentException("A submission version is required.", nameof(submissionVersionId));
        if (reviewerUserId == Guid.Empty) throw new ArgumentException("A reviewer is required.", nameof(reviewerUserId));
        if (!Enum.IsDefined(decision)) throw new ArgumentOutOfRangeException(nameof(decision));
        if (string.IsNullOrWhiteSpace(feedback)) throw new ArgumentException("Technical review feedback is required.", nameof(feedback));
        if (criteriaNotes?.Length > 8000) throw new ArgumentException("Criteria notes cannot exceed 8000 characters.", nameof(criteriaNotes));

        SubmissionId = submissionId;
        SubmissionVersionId = submissionVersionId;
        ReviewerUserId = reviewerUserId;
        Decision = decision;
        Feedback = feedback.Trim();
        CriteriaNotes = string.IsNullOrWhiteSpace(criteriaNotes) ? null : criteriaNotes.Trim();
    }
}
