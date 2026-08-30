using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Workspaces;

public sealed class TaskComment : AuditableEntity
{
    public Guid ProjectTaskId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Content { get; private set; } = string.Empty;

    private TaskComment() { }

    public TaskComment(Guid projectTaskId, Guid authorUserId, string content)
    {
        ProjectTaskId = projectTaskId;
        AuthorUserId = authorUserId;
        Content = string.IsNullOrWhiteSpace(content) ? throw new ArgumentException("Comment content is required.", nameof(content)) : content.Trim();
    }
}

public sealed class TaskChecklistItem : AuditableEntity
{
    public Guid ProjectTaskId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public int SortOrder { get; private set; }

    private TaskChecklistItem() { }

    public TaskChecklistItem(Guid projectTaskId, string title, int sortOrder)
    {
        ProjectTaskId = projectTaskId;
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Checklist title is required.", nameof(title)) : title.Trim();
        SortOrder = sortOrder;
    }

    public void Update(string title, bool isCompleted)
    {
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Checklist title is required.", nameof(title)) : title.Trim();
        IsCompleted = isCompleted;
    }
}
