using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.Applications;

public sealed record TeamApplicationMemberInput(
    [Required] Guid StudentId,
    [StringLength(100)] string? ProposedRole);

public sealed record CreateApplicationRequest
{
    /// <summary>Optional team owned by the applicant; accepted teams are locked server-side.</summary>
    public Guid? TeamId { get; init; }

    [StringLength(4000, ErrorMessage = "Cover letter must be at most 4000 characters.")]
    public string? CoverLetter { get; init; }

    [StringLength(500)]
    public string? GithubUrl { get; init; }

    [StringLength(500)]
    public string? PortfolioUrl { get; init; }

    [StringLength(1000)]
    public string? CvUrl { get; init; }

    [StringLength(255)]
    public string? CvFileName { get; init; }

    [StringLength(200)]
    public string? Availability { get; init; }

    public bool RequireMemberConsent { get; init; }

    public IReadOnlyCollection<TeamApplicationMemberInput>? Members { get; init; }

    public Guid? RecommendedByLecturerId { get; init; }
}

public sealed record WithdrawApplicationRequest
{
    [StringLength(1000, ErrorMessage = "Withdraw reason must be at most 1000 characters.")]
    public string? WithdrawReason { get; init; }
}

public sealed record MemberConsentRequest(bool Approve, [StringLength(500)] string? Note = null);

public sealed record LeaderJoinApprovalRequest(bool Approve, [StringLength(500)] string? Note = null);

public sealed record ResolveApplicationConflictRequest(bool Preferred, int? PriorityOrder = null, [StringLength(500)] string? Note = null, Guid? WithdrawOtherApplicationId = null);

public sealed record ApplicationMemberResponse(
    Guid Id,
    Guid StudentId,
    Guid UserId,
    string DisplayName,
    string? StudentCode,
    string Email,
    string? AvatarUrl,
    string? ProposedRole,
    string ConsentStatus,
    bool IsSelectedByCompany,
    DateTimeOffset? RespondedAt,
    string? Note,
    IReadOnlyCollection<string> Skills);

public sealed record ApplicationConflictItemResponse(
    Guid ApplicationId,
    Guid ProjectId,
    string ProjectTitle,
    string ApplicationType,
    string Status,
    string RoleInApplication,
    bool? IsPreferredInConflict,
    int? PriorityOrder);

public sealed record ApplicationResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectTitle,
    string ProjectSlug,
    string CompanyName,
    string Status,
    string? CoverLetter,
    DateTimeOffset CreatedAt,
    DateTimeOffset? WithdrawnAt,
    string ApplicationType = "INDIVIDUAL",
    Guid? TeamId = null,
    string? TeamName = null,
    Guid? LeaderStudentId = null,
    string? LeaderName = null,
    string? GithubUrl = null,
    string? PortfolioUrl = null,
    string? CvUrl = null,
    string? CvFileName = null,
    string? Availability = null,
    Guid? RecommendedByLecturerId = null,
    string? RecommendedByLecturerName = null,
    string? InterviewScheduleNote = null,
    string? DecisionReason = null,
    string? AdjustmentProposalJson = null,
    string? CompanyOfferConditions = null,
    bool HasConflict = false,
    string? ConflictSummary = null,
    bool? IsPreferredInConflict = null,
    int? PriorityOrder = null,
    IReadOnlyCollection<ApplicationMemberResponse>? Members = null,
    Guid? JoinTargetTeamId = null,
    string? JoinTargetTeamName = null,
    string? JoinProposedRole = null,
    string? TeamAvatarUrl = null,
    string? JoinTargetTeamAvatarUrl = null,
    Guid CompanyId = default);

public sealed class ApplicationListQuery : PageQuery
{
    /// <summary>Optional status filter (e.g. PENDING, WITHDRAWN); invalid values are ignored.</summary>
    public string? Status { get; init; }
}
