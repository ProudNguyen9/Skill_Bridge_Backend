using System.ComponentModel.DataAnnotations;

namespace DNTU.SkillBridge.Api.Academics;

public sealed class CriterionScoreRequest
{
    public Guid RubricCriterionId { get; init; }

    [Range(typeof(decimal), "0", "100")]
    public decimal Score { get; init; }
}

public sealed class UpsertAcademicEvaluationRequest
{
    public Guid RubricId { get; init; }
    public Guid? Version { get; init; }

    [Required, MinLength(1)]
    public IReadOnlyCollection<CriterionScoreRequest> Scores { get; init; } = [];
}

public sealed record AcademicEvaluationResponse(
    Guid Id,
    Guid ProjectId,
    Guid StudentId,
    Guid RubricId,
    decimal TotalScore,
    bool IsPassed,
    string Status,
    Guid Version,
    DateTimeOffset? FinalizedAt);
