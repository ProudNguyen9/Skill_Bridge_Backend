using DNTU.SkillBridge.Domain.Common;

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

public sealed class ProjectCommitment : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public Guid StudentId { get; private set; }
    public CommitmentStatus Status { get; private set; } = CommitmentStatus.PENDING;
    public string PolicyVersion { get; private set; } = "v1";
    public DateTimeOffset ConfirmationDeadline { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }

    private ProjectCommitment() { }

    public ProjectCommitment(Guid projectId, Guid studentId, DateTimeOffset confirmationDeadline, string policyVersion = "v1")
    {
        ProjectId = projectId;
        StudentId = studentId;
        ConfirmationDeadline = confirmationDeadline;
        PolicyVersion = policyVersion;
    }

    public void Confirm(DateTimeOffset now)
    {
        if (Status != CommitmentStatus.PENDING || ConfirmationDeadline < now)
        {
            throw new InvalidOperationException("This commitment can no longer be confirmed.");
        }

        Status = CommitmentStatus.CONFIRMED;
        ConfirmedAt = now;
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status == CommitmentStatus.PENDING && ConfirmationDeadline <= now)
        {
            Status = CommitmentStatus.ABANDONED;
        }
    }
}
