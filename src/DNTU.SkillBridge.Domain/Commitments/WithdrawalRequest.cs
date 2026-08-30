using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Commitments;

public enum WithdrawalStatus
{
    REQUESTED = 1,
    RECOMMENDED = 2,
    APPROVED = 3,
    REJECTED = 4,
    ABANDONED = 5
}

/// <summary>Append-only governed departure request for an active project member.</summary>
public sealed class WithdrawalRequest : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public Guid StudentId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public WithdrawalStatus Status { get; private set; } = WithdrawalStatus.REQUESTED;
    public Guid? RecommendedByLecturerId { get; private set; }
    public DateTimeOffset? RecommendedAt { get; private set; }
    public string? LecturerNote { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    private WithdrawalRequest() { }

    public WithdrawalRequest(Guid projectId, Guid studentId, string reason)
    {
        ProjectId = projectId;
        StudentId = studentId;
        Reason = string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("A withdrawal reason is required.", nameof(reason))
            : reason.Trim();
    }

    public void Recommend(Guid lecturerId, DateTimeOffset at, string? note)
    {
        if (Status != WithdrawalStatus.REQUESTED)
        {
            throw new InvalidOperationException("Only requested withdrawals can be recommended.");
        }

        Status = WithdrawalStatus.RECOMMENDED;
        RecommendedByLecturerId = lecturerId;
        RecommendedAt = at;
        LecturerNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void Decide(Guid userId, DateTimeOffset at, bool approve, string? note)
    {
        if (Status is not (WithdrawalStatus.REQUESTED or WithdrawalStatus.RECOMMENDED))
        {
            throw new InvalidOperationException("This withdrawal has already been decided.");
        }

        Status = approve ? WithdrawalStatus.APPROVED : WithdrawalStatus.REJECTED;
        DecidedByUserId = userId;
        DecidedAt = at;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
