using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Students;

namespace DNTU.SkillBridge.Api.Students;

public sealed class VerifySkillRequest
{
    [Required] public Guid ProjectId { get; init; }
    [Required] public SkillLevel Level { get; init; }
    public Guid? EvidenceSubmissionId { get; init; }
}

public sealed class UpsertPortfolioEntryRequest
{
    [Required, StringLength(4000, MinimumLength = 2)]
    public string Summary { get; init; } = string.Empty;
}

public sealed record VerifiedSkillResponse(
    Guid Id,
    Guid StudentId,
    Guid SkillId,
    string SkillName,
    Guid ProjectId,
    string ProjectTitle,
    SkillLevel Level,
    bool IsRevoked,
    DateTimeOffset VerifiedAt,
    DateTimeOffset? RevokedAt,
    Guid? EvidenceSubmissionId,
    Guid? EvidenceEvaluationId);

public sealed record PortfolioEntryResponse(
    Guid Id,
    Guid StudentId,
    Guid ProjectId,
    string ProjectTitle,
    string Summary,
    bool IsPublished);

public sealed record SkillPassportResponse(
    Guid StudentId,
    IReadOnlyCollection<VerifiedSkillResponse> VerifiedSkills,
    IReadOnlyCollection<PortfolioEntryResponse> Portfolio);

