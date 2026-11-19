using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Commitments;

namespace DNTU.SkillBridge.Application.Commitments;

public sealed class SignCommitmentRequest
{
    [StringLength(150)]
    public string? SignerName { get; init; }

    [StringLength(150)]
    public string? SignerTitle { get; init; }

    [StringLength(500000)]
    public string? SignatureDataUrl { get; init; }
}

public sealed record PartySignatureInfoResponse(
    bool Signed,
    string? SignerName,
    string? SignerTitle,
    string? SignatureDataUrl,
    DateTimeOffset? SignedAt);

public sealed record CommitmentResponse(
    Guid Id,
    Guid ProjectId,
    Guid StudentId,
    CommitmentStatus Status,
    string PolicyVersion,
    DateTimeOffset ConfirmationDeadline,
    DateTimeOffset? ConfirmedAt,
    Guid? ApplicationId = null,
    string? ProposedRole = null,
    string? Conditions = null,
    long? AllowanceShareAmount = null,
    DateTimeOffset? DeclinedAt = null,
    string? DeclineReason = null,
    string? StudentSignerName = null,
    string? StudentSignatureDataUrl = null,
    PartySignatureInfoResponse? CompanySignature = null,
    PartySignatureInfoResponse? UniversitySignature = null);

public sealed class DeclineCommitmentRequest
{
    [StringLength(1000)]
    public string? Reason { get; init; }
}

public sealed record ProjectCommitmentMemberItemResponse(
    Guid CommitmentId,
    Guid StudentId,
    string DisplayName,
    string? StudentCode,
    string Email,
    CommitmentStatus Status,
    string? ProposedRole,
    long? AllowanceShareAmount,
    DateTimeOffset ConfirmationDeadline,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? DeclinedAt,
    string? DeclineReason,
    string? StudentSignerName = null,
    string? StudentSignatureDataUrl = null);

public sealed record ProjectCommitmentOverviewResponse(
    Guid ProjectId,
    string ProjectTitle,
    string CompanyName,
    long? TotalAllowanceAmount,
    string? AllowanceCurrency,
    string? SupervisorName,
    string? SupervisorStatus,
    long? SupervisorAllowanceShareAmount,
    IReadOnlyCollection<ProjectCommitmentMemberItemResponse> Members,
    string? ProjectCode = null,
    string? ProjectStatus = null,
    string? Conditions = null,
    string? PolicyVersion = "v1",
    PartySignatureInfoResponse? CompanySignature = null,
    PartySignatureInfoResponse? UniversitySignature = null);

public sealed record ProjectCommitmentSummaryItemResponse(
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    string ProjectStatus,
    Guid CompanyId,
    string CompanyName,
    int TotalStudentCommitments,
    int ConfirmedStudentCount,
    int PendingStudentCount,
    bool CompanySigned,
    string? CompanySignerName,
    DateTimeOffset? CompanySignedAt,
    bool UniversitySigned,
    string? UniversitySignerName,
    DateTimeOffset? UniversitySignedAt);

public sealed class CreateWithdrawalRequest
{
    [Required]
    public Guid ProjectId { get; init; }

    [Required, StringLength(1000, MinimumLength = 3)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class WithdrawalDecisionRequest
{
    [StringLength(1000)]
    public string? Note { get; init; }
}

public sealed record WithdrawalResponse(
    Guid Id,
    Guid ProjectId,
    Guid StudentId,
    string Reason,
    WithdrawalStatus Status,
    Guid? RecommendedByLecturerId,
    DateTimeOffset? RecommendedAt,
    string? LecturerNote,
    Guid? DecidedByUserId,
    DateTimeOffset? DecidedAt,
    string? DecisionNote,
    DateTimeOffset CreatedAt);

public sealed class AdminWithdrawalListQuery : PageQuery
{
    public string? Status { get; init; }
}

/// <summary>Admin-scoped withdrawal queue row enriched with student/project display fields.</summary>
public sealed record AdminWithdrawalListItemResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectTitle,
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    string Reason,
    WithdrawalStatus Status,
    string? LecturerNote,
    DateTimeOffset? RecommendedAt,
    DateTimeOffset? DecidedAt,
    string? DecisionNote,
    DateTimeOffset CreatedAt);
