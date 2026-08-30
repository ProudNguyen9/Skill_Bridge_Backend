using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Api.Contracts;

namespace DNTU.SkillBridge.Api.Applications;

public sealed class CompanyApplicationListQuery : PageQuery
{
    public Guid? ProjectId { get; init; }
    public string? Status { get; init; }
}

/// <summary>
/// Company-only application projection. Profile links are intentionally never exposed
/// from student/public endpoints and are scoped by the owning project's company.
/// </summary>
public sealed record CompanyApplicationResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectTitle,
    string ProjectSlug,
    string Status,
    string ApplicantDisplayName,
    string? StudentCode,
    string? CvUrl,
    string? PortfolioUrl,
    string? GithubUrl,
    string? Proposal,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt,
    string? DecisionReason);

public sealed class ApplicationDecisionRequest
{
    [StringLength(1000)]
    public string? Reason { get; init; }
}
