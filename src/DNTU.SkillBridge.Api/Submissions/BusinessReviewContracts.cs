using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.Api.Submissions;

/// <summary>Company-owned business assessment input. Academic scoring and verified-skill fields are deliberately absent.</summary>
public sealed class CreateBusinessReviewRequest
{
    [Required]
    public Guid Version { get; init; }

    [EnumDataType(typeof(BusinessReviewDecision))]
    public BusinessReviewDecision Decision { get; init; }

    [Required, StringLength(4000)]
    public string RequirementsFeedback { get; init; } = string.Empty;

    [StringLength(4000)]
    public string? CollaborationFeedback { get; init; }
}

/// <summary>Immutable business-review record safe to expose only in the owning company/project scope.</summary>
public sealed record BusinessReviewResponse(
    Guid Id,
    Guid SubmissionId,
    Guid SubmissionVersionId,
    Guid CompanyId,
    Guid ReviewerUserId,
    BusinessReviewDecision Decision,
    string RequirementsFeedback,
    string CollaborationFeedback,
    DateTimeOffset CreatedAt);
