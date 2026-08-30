using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Meetings;

public sealed class MeetingMinute : AuditableEntity
{
    public Guid MeetingId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public Guid UpdatedByUserId { get; private set; }

    private MeetingMinute() { }
    public MeetingMinute(Guid meetingId, string content, Guid updatedByUserId) => (MeetingId, Content, UpdatedByUserId) = (meetingId, Required(content), updatedByUserId);
    public void Update(string content, Guid updatedByUserId) => (Content, UpdatedByUserId) = (Required(content), updatedByUserId);
    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 10000 ? value.Trim() : throw new ArgumentException("Meeting minutes must contain at most 10,000 characters.", nameof(value));
}

/// <summary>Immutable audit snapshot created whenever meeting minutes are replaced.</summary>
public sealed class MeetingMinuteRevision : AuditableEntity
{
    public Guid MeetingMinuteId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public Guid EditedByUserId { get; private set; }

    private MeetingMinuteRevision() { }
    public MeetingMinuteRevision(Guid meetingMinuteId, string content, Guid editedByUserId) => (MeetingMinuteId, Content, EditedByUserId) = (meetingMinuteId, content, editedByUserId);
}

public sealed class MeetingActionItem : AuditableEntity
{
    public Guid MeetingId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid? ResponsibleStudentId { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public bool IsCompleted { get; private set; }
    public Guid? ProjectTaskId { get; private set; }
    public Guid Version { get; private set; } = Guid.CreateVersion7();

    private MeetingActionItem() { }
    public MeetingActionItem(Guid meetingId, string description, Guid? responsibleStudentId, DateTimeOffset? dueAt)
    {
        MeetingId = meetingId;
        Description = Required(description);
        ResponsibleStudentId = responsibleStudentId;
        DueAt = dueAt;
    }

    public void Update(string description, Guid? responsibleStudentId, DateTimeOffset? dueAt, Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        Description = Required(description);
        ResponsibleStudentId = responsibleStudentId;
        DueAt = dueAt;
        RenewVersion();
    }

    public void SetCompleted(bool isCompleted, Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        IsCompleted = isCompleted;
        RenewVersion();
    }

    public void LinkTask(Guid taskId)
    {
        if (ProjectTaskId.HasValue) throw new InvalidOperationException("This action item is already linked to a task.");
        ProjectTaskId = taskId;
        RenewVersion();
    }

    private void EnsureVersion(Guid expectedVersion)
    {
        if (expectedVersion == Guid.Empty || Version != expectedVersion) throw new InvalidOperationException("The action item has changed; reload it and retry.");
    }

    private void RenewVersion() => Version = Guid.CreateVersion7();
    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 2000 ? value.Trim() : throw new ArgumentException("Action item description must contain at most 2,000 characters.", nameof(value));
}
