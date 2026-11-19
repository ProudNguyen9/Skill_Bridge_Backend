using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Commitments;

public sealed class ProjectCommitment : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public CommitmentStatus Status { get; private set; } = CommitmentStatus.PENDING;
    public string PolicyVersion { get; private set; } = "v1";
    public string? ProposedRole { get; private set; }
    public string? Conditions { get; private set; }
    public long? AllowanceShareAmount { get; private set; }
    public DateTimeOffset ConfirmationDeadline { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string? StudentSignerName { get; private set; }
    public string? StudentSignatureData { get; private set; }
    public DateTimeOffset? DeclinedAt { get; private set; }
    public string? DeclineReason { get; private set; }

    private ProjectCommitment() { }

    public ProjectCommitment(
        Guid projectId,
        Guid studentId,
        DateTimeOffset confirmationDeadline,
        string policyVersion = "v1",
        Guid? applicationId = null,
        string? proposedRole = null,
        string? conditions = null,
        long? allowanceShareAmount = null)
    {
        ProjectId = projectId;
        StudentId = studentId;
        ApplicationId = applicationId;
        ConfirmationDeadline = confirmationDeadline;
        PolicyVersion = policyVersion;
        ProposedRole = string.IsNullOrWhiteSpace(proposedRole) ? null : proposedRole.Trim();
        Conditions = string.IsNullOrWhiteSpace(conditions) ? null : conditions.Trim();
        AllowanceShareAmount = allowanceShareAmount;
    }

    public void Confirm(DateTimeOffset now) => Confirm(now, null, null);

    public void Confirm(DateTimeOffset now, string? signerName, string? signatureData)
    {
        if (Status != CommitmentStatus.PENDING || ConfirmationDeadline < now)
        {
            throw new InvalidOperationException("This commitment can no longer be confirmed.");
        }

        Status = CommitmentStatus.CONFIRMED;
        ConfirmedAt = now;
        StudentSignerName = string.IsNullOrWhiteSpace(signerName) ? null : signerName.Trim();
        StudentSignatureData = string.IsNullOrWhiteSpace(signatureData) ? null : signatureData.Trim();
    }

    public void Decline(DateTimeOffset now, string? reason = null)
    {
        if (Status != CommitmentStatus.PENDING)
        {
            throw new InvalidOperationException("Only pending commitments can be declined.");
        }

        Status = CommitmentStatus.DECLINED;
        DeclinedAt = now;
        DeclineReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status == CommitmentStatus.PENDING && ConfirmationDeadline <= now)
        {
            Status = CommitmentStatus.ABANDONED;
        }
    }
}
