using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.Applications;

public sealed class CompanyApplicationListQuery : PageQuery
{
    public Guid? ProjectId { get; init; }
    public string? Status { get; init; }
    public string? ApplicationType { get; init; }
}

/// <summary>
/// Company-only application projection including team roster, skills, attachments, recommendation source, and conflict flags.
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
    string? DecisionReason,
    string ApplicationType = "INDIVIDUAL",
    Guid? TeamId = null,
    string? TeamName = null,
    string? TeamDescription = null,
    string? ApplicantEmail = null,
    string? ApplicantMajor = null,
    string? ApplicantAvatarUrl = null,
    string? CvFileName = null,
    string? Availability = null,
    IReadOnlyCollection<string>? ApplicantSkills = null,
    IReadOnlyCollection<ApplicationMemberResponse>? Members = null,
    Guid? RecommendedByLecturerId = null,
    string? RecommendedByLecturerName = null,
    string? RecommendedByLecturerDepartment = null,
    string? RecommendationReason = null,
    bool HasConflict = false,
    string? ConflictWarning = null,
    string? InterviewScheduleNote = null,
    string? AdjustmentProposalJson = null,
    string? CompanyOfferConditions = null,
    Guid? StudentId = null,
    Guid? JoinTargetTeamId = null,
    string? JoinTargetTeamName = null,
    string? JoinProposedRole = null,
    string? TeamAvatarUrl = null,
    string? JoinTargetTeamAvatarUrl = null,
    Guid? ApplicantUserId = null);

public sealed record CompanyProjectApplicationSummaryResponse(
    Guid ProjectId,
    int TotalCount,
    int PendingCount,
    int ShortlistedCount,
    int AdjustmentRequestedCount,
    int AcceptedCount,
    int RejectedCount,
    int TeamCount,
    int IndividualCount,
    int AcceptedStudentCount = 0,
    int ExpectedStudentCount = 0,
    Guid? AcceptedTeamId = null,
    string? AcceptedTeamName = null,
    Guid? AcceptedTeamLeaderStudentId = null,
    string? AcceptedTeamLeaderName = null,
    int WaitingLeaderApprovalCount = 0,
    string? AcceptedTeamAvatarUrl = null);

public sealed class RequestJoinTeamRequest
{
    [StringLength(100)]
    public string? ProposedRole { get; init; }

    [StringLength(1000)]
    public string? Reason { get; init; }

    [StringLength(2000)]
    public string? Conditions { get; init; }

    [Range(1, 30)]
    public int? ConfirmationDeadlineDays { get; init; }
}

public sealed class ApplicationDecisionRequest
{
    [StringLength(1000)]
    public string? Reason { get; init; }

    [StringLength(1000)]
    public string? InterviewScheduleNote { get; init; }

    [StringLength(2000)]
    public string? Conditions { get; init; }

    [Range(1, 30)]
    public int? ConfirmationDeadlineDays { get; init; }
}

public sealed class RequestAdjustmentRequest
{
    [Required, StringLength(1000, MinimumLength = 3)]
    public string Reason { get; init; } = string.Empty;

    public IReadOnlyCollection<Guid>? SelectedStudentIds { get; init; }

    public IDictionary<Guid, string>? MemberRoles { get; init; }
}

public sealed class MergeIndividualApplicationsRequest
{
    [Required, MinLength(2)]
    public IReadOnlyCollection<Guid> ApplicationIds { get; init; } = [];

    [Required, StringLength(120, MinimumLength = 2)]
    public string TeamName { get; init; } = string.Empty;

    [Required]
    public Guid LeaderStudentId { get; init; }

    public IDictionary<Guid, string>? MemberRoles { get; init; }

    [StringLength(2000)]
    public string? Conditions { get; init; }

    [Range(1, 30)]
    public int ConfirmationDeadlineDays { get; init; } = 7;
}
