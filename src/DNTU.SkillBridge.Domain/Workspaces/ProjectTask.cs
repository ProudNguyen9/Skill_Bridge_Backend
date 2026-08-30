using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Workspaces;

public enum ProjectTaskStatus
{
    BACKLOG = 1,
    TODO = 2,
    IN_PROGRESS = 3,
    REVIEW = 4,
    DONE = 5
}

public enum ProjectTaskPriority
{
    LOW = 1,
    MEDIUM = 2,
    HIGH = 3,
    URGENT = 4
}

/// <summary>Workspace Kanban task with an optimistic-concurrency version.</summary>
public sealed class ProjectTask : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ProjectTaskStatus Status { get; private set; } = ProjectTaskStatus.BACKLOG;
    public ProjectTaskPriority Priority { get; private set; } = ProjectTaskPriority.MEDIUM;
    public Guid? AssigneeStudentId { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public int SortOrder { get; private set; }
    public Guid Version { get; private set; } = Guid.CreateVersion7();
    public bool IsDeleted { get; private set; }

    private ProjectTask() { }

    public ProjectTask(Guid projectId, string title, string? description, ProjectTaskPriority priority, DateTimeOffset? dueAt, int sortOrder)
    {
        ProjectId = projectId;
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        Priority = priority;
        DueAt = dueAt;
        SortOrder = sortOrder;
    }

    public void Update(string title, string? description, ProjectTaskPriority priority, DateTimeOffset? dueAt, Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        Priority = priority;
        DueAt = dueAt;
        RenewVersion();
    }

    public void Move(ProjectTaskStatus status, int sortOrder, Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        Status = status;
        SortOrder = sortOrder;
        RenewVersion();
    }

    public void Assign(Guid? studentId, Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        AssigneeStudentId = studentId;
        RenewVersion();
    }

    /// <summary>Advances the task version when a checklist completion mutation is applied.</summary>
    public void UpdateChecklistVersion(Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        RenewVersion();
    }

    public void Delete(Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        IsDeleted = true;
        RenewVersion();
    }

    private void EnsureVersion(Guid expectedVersion)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("The task has changed; reload it and retry.");
    }

    private void RenewVersion() => Version = Guid.CreateVersion7();
    private static string NormalizeTitle(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A task title is required.", nameof(value)) : value.Trim();
    private static string? NormalizeDescription(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
