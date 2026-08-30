using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

/// <summary>Data operations for task comments, checklist items, and task-scoped paging queries.</summary>
public interface ITaskCollaborationRepository
{
    void AddComment(TaskComment comment);

    Task<IReadOnlyCollection<TaskCommentResponse>> ListCommentsAsync(Guid taskId, CancellationToken cancellationToken);

    Task<TaskComment?> FindCommentAsync(Guid commentId, Guid taskId, CancellationToken cancellationToken);

    void RemoveComment(TaskComment comment);

    Task<TaskChecklistItem?> FindChecklistItemAsync(Guid itemId, CancellationToken cancellationToken);

    Task<int?> GetMaxChecklistSortOrderAsync(Guid taskId, CancellationToken cancellationToken);

    void AddChecklistItem(TaskChecklistItem item);

    void RemoveChecklistItem(TaskChecklistItem item);

    /// <summary>Re-attaches a task as modified, restoring the client version as EF's original concurrency value.</summary>
    void MarkTaskForChecklistUpdate(ProjectTask task, Guid expectedVersion);

    /// <summary>Persists pending changes; returns false when an optimistic-concurrency conflict is detected.</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);

    Task<ProjectTask?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken);

    Task<bool> HasCompanyManagerAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<(int TotalCount, IReadOnlyCollection<ProjectTaskResponse> Items)> ListStudentTasksAsync(Guid userId, TaskListQuery query, CancellationToken cancellationToken);

    Task<(int TotalCount, IReadOnlyCollection<ProjectTaskResponse> Items)> ListLecturerTasksAsync(Guid userId, TaskListQuery query, CancellationToken cancellationToken);
}
