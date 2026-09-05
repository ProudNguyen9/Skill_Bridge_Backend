using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Submissions;

/// <summary>Immutable company-only business assessment with no academic fields or scores.</summary>
public sealed class BusinessSubmissionReview : AuditableEntity
{
    public Guid SubmissionId { get; private set; }
    public Guid SubmissionVersionId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid ReviewerUserId { get; private set; }
    public BusinessReviewDecision Decision { get; private set; }
    public string RequirementsFeedback { get; private set; } = string.Empty;
    public string CollaborationFeedback { get; private set; } = string.Empty;

    private BusinessSubmissionReview() { }

    public BusinessSubmissionReview(
        Guid submissionId,
        Guid submissionVersionId,
        Guid companyId,
        Guid reviewerUserId,
        BusinessReviewDecision decision,
        string requirementsFeedback,
        string collaborationFeedback)
    {
        if (submissionId == Guid.Empty || submissionVersionId == Guid.Empty || companyId == Guid.Empty || reviewerUserId == Guid.Empty)
        {
            throw new ArgumentException("Business review identifiers are required.");
        }

        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        if (string.IsNullOrWhiteSpace(requirementsFeedback))
        {
            throw new ArgumentException("Requirements feedback is required.", nameof(requirementsFeedback));
        }

        if (decision == BusinessReviewDecision.REVISION_REQUIRED && string.IsNullOrWhiteSpace(collaborationFeedback))
        {
            throw new ArgumentException("Revision rationale is required.", nameof(collaborationFeedback));
        }

        SubmissionId = submissionId;
        SubmissionVersionId = submissionVersionId;
        CompanyId = companyId;
        ReviewerUserId = reviewerUserId;
        Decision = decision;
        RequirementsFeedback = requirementsFeedback.Trim();
        CollaborationFeedback = collaborationFeedback?.Trim() ?? string.Empty;
    }
}
