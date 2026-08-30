using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.Application.Submissions;

public enum TechnicalReviewOutcome
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    Invalid
}

public sealed class CreateTechnicalReviewRequest
{
    /// <summary>Current submission concurrency token returned by the submission API.</summary>
    [Required] public Guid Version { get; init; }

    [EnumDataType(typeof(TechnicalReviewDecision))]
    public TechnicalReviewDecision Decision { get; init; }

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Feedback { get; init; } = string.Empty;

    /// <summary>Optional immutable rubric-ready JSON or structured review notes.</summary>
    [StringLength(8000)]
    public string? CriteriaNotes { get; init; }
}

public sealed record TechnicalReviewResponse(
    Guid Id,
    Guid SubmissionId,
    Guid SubmissionVersionId,
    Guid ReviewerUserId,
    TechnicalReviewDecision Decision,
    string Feedback,
    string? CriteriaNotes,
    DateTimeOffset CreatedAt);
