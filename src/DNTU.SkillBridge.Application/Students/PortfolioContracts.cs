using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Students;

namespace DNTU.SkillBridge.Application.Students;

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
    Guid? EvidenceEvaluationId,
    string? ProofHash = null);

public sealed record PortfolioEntryResponse(
    Guid Id,
    Guid StudentId,
    Guid ProjectId,
    string ProjectTitle,
    string Summary,
    bool IsPublished);

public sealed record SkillPassportCertificateResponse(
    Guid ProjectId,
    string ProjectTitle,
    string? ProjectCode,
    string? CompanyName,
    decimal FinalScore,
    string Grade,
    string ProofHash,
    DateTimeOffset FinalizedAt);

public sealed record SkillPassportResponse(
    Guid StudentId,
    IReadOnlyCollection<VerifiedSkillResponse> VerifiedSkills,
    IReadOnlyCollection<PortfolioEntryResponse> Portfolio,
    IReadOnlyCollection<SkillPassportCertificateResponse>? Certificates = null,
    string? PrimaryProofHash = null);

