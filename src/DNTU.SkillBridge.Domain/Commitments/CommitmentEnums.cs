namespace DNTU.SkillBridge.Domain.Commitments;

public enum CommitmentStatus
{
    PENDING = 1,
    CONFIRMED = 2,
    /// <summary>
    /// Retained for commitments expired before the abandonment policy was introduced.
    /// New overdue commitments transition to <see cref="ABANDONED"/>.
    /// </summary>
    EXPIRED = 3,
    ABANDONED = 4
}

public enum WithdrawalStatus
{
    REQUESTED = 1,
    RECOMMENDED = 2,
    APPROVED = 3,
    REJECTED = 4,
    ABANDONED = 5
}
