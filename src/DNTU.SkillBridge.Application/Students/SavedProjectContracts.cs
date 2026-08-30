namespace DNTU.SkillBridge.Application.Students;

/// <summary>One row of the caller's saved-project list (most recently saved first).</summary>
public sealed record SavedProjectListItemResponse(
    Guid ProjectId,
    string Title,
    string Slug,
    string CompanyName,
    string Status,
    string Difficulty,
    int DurationWeeks,
    long? AllowanceAmount,
    string? AllowanceCurrency,
    DateTimeOffset SavedAt);

/// <summary>
/// Informational match between the caller's active declared skills and a project's
/// skill requirements; never an application eligibility decision.
/// </summary>
public sealed record SkillMatchResponse(
    Guid ProjectId,
    int RequiredCount,
    int MatchedCount,
    int MatchPercent,
    IReadOnlyCollection<SkillMatchItemResponse> Matched,
    IReadOnlyCollection<SkillMatchItemResponse> Missing);

public sealed record SkillMatchItemResponse(Guid SkillId, string SkillName);
