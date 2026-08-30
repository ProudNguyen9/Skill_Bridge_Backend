using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Api.Contracts;

namespace DNTU.SkillBridge.Api.Applications;

public sealed record CreateApplicationRequest
{
    /// <summary>Optional team owned by the applicant; accepted teams are locked server-side.</summary>
    public Guid? TeamId { get; init; }

    [StringLength(4000, ErrorMessage = "Cover letter must be at most 4000 characters.")]
    public string? CoverLetter { get; init; }
}

public sealed record WithdrawApplicationRequest
{
    [StringLength(1000, ErrorMessage = "Withdraw reason must be at most 1000 characters.")]
    public string? WithdrawReason { get; init; }
}

public sealed record ApplicationResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectTitle,
    string ProjectSlug,
    string CompanyName,
    string Status,
    string? CoverLetter,
    DateTimeOffset CreatedAt,
    DateTimeOffset? WithdrawnAt);

public sealed class ApplicationListQuery : PageQuery
{
    /// <summary>Optional status filter (e.g. PENDING, WITHDRAWN); invalid values are ignored.</summary>
    public string? Status { get; init; }
}
