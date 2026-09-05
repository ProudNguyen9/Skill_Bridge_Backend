using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Milestones;

/// <summary>Append-only audit entry for every successful milestone state change.</summary>
public sealed class MilestoneApprovalHistory : AuditableEntity
{
    public Guid MilestoneId { get; private set; }
    public MilestoneStatus FromStatus { get; private set; }
    public MilestoneStatus ToStatus { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string? Note { get; private set; }

    private MilestoneApprovalHistory() { }

    public MilestoneApprovalHistory(Guid milestoneId, MilestoneStatus fromStatus, MilestoneStatus toStatus, Guid actorUserId, string? note)
    {
        MilestoneId = milestoneId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorUserId = actorUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

public sealed class ProjectMilestone : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Sequence { get; private set; }
    public DateTimeOffset DueAt { get; private set; }
    public MilestoneStatus Status { get; private set; } = MilestoneStatus.PLANNED;
    public Guid Version { get; private set; } = Guid.CreateVersion7();

    private ProjectMilestone() { }

    public ProjectMilestone(Guid projectId, string title, string? description, int sequence, DateTimeOffset dueAt)
    {
        ProjectId = projectId;
        Title = Normalize(title);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Sequence = sequence;
        DueAt = dueAt;
    }

    public void Update(string title, string? description, int sequence, DateTimeOffset dueAt, Guid expectedVersion)
    {
        EnsureMutable(expectedVersion);
        Title = Normalize(title);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Sequence = sequence;
        DueAt = dueAt;
        Version = Guid.CreateVersion7();
    }

    public void Start(Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status is not (MilestoneStatus.PLANNED or MilestoneStatus.REVISION_REQUIRED))
        {
            throw new InvalidOperationException($"Cannot start a milestone from {Status}.");
        }

        Status = MilestoneStatus.IN_PROGRESS;
        Version = Guid.CreateVersion7();
    }

    public void Submit(Guid expectedVersion) => Transition(MilestoneStatus.IN_PROGRESS, MilestoneStatus.SUBMITTED, expectedVersion);
    public void RequestRevision(Guid expectedVersion) => Transition(MilestoneStatus.SUBMITTED, MilestoneStatus.REVISION_REQUIRED, expectedVersion);
    public void Approve(Guid expectedVersion) => Transition(MilestoneStatus.SUBMITTED, MilestoneStatus.APPROVED, expectedVersion);

    private void EnsureMutable(Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status == MilestoneStatus.APPROVED) throw new InvalidOperationException("Approved milestones are immutable.");
    }

    private void EnsureVersion(Guid expectedVersion)
    {
        if (expectedVersion == Guid.Empty || Version != expectedVersion) throw new InvalidOperationException("The milestone was changed by another request.");
    }

    private void Transition(MilestoneStatus from, MilestoneStatus to, Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status != from) throw new InvalidOperationException($"Cannot transition milestone from {Status} to {to}.");
        Status = to;
        Version = Guid.CreateVersion7();
    }

    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A milestone title is required.", nameof(value)) : value.Trim();
}

public sealed class MilestoneDeliverable : AuditableEntity
{
    public Guid MilestoneId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Criteria { get; private set; }
    public Guid? FileId { get; private set; }

    private MilestoneDeliverable() { }

    public MilestoneDeliverable(Guid milestoneId, string name, string? criteria, Guid? fileId)
    {
        MilestoneId = milestoneId;
        Name = Normalize(name);
        Criteria = string.IsNullOrWhiteSpace(criteria) ? null : criteria.Trim();
        FileId = fileId;
    }

    public void Update(string name, string? criteria, Guid? fileId)
    {
        Name = Normalize(name);
        Criteria = string.IsNullOrWhiteSpace(criteria) ? null : criteria.Trim();
        FileId = fileId;
    }

    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A deliverable name is required.", nameof(value)) : value.Trim();
}
