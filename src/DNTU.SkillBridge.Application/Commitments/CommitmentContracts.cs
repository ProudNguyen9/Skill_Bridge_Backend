using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Commitments;

namespace DNTU.SkillBridge.Application.Commitments;

public enum WithdrawalOutcome
{
    Created,
    NotFound,
    Conflict,
    Forbidden,
    Updated
}

public sealed record CommitmentResponse(Guid Id, Guid ProjectId, Guid StudentId, CommitmentStatus Status, string PolicyVersion, DateTimeOffset ConfirmationDeadline, DateTimeOffset? ConfirmedAt);

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
