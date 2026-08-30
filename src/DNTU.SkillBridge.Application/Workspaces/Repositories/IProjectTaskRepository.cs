using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

/// <summary>Data operations for kanban project tasks and their project membership checks.</summary>
public interface IProjectTaskRepository
{
    Task<bool> IsMemberAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<int?> GetMaxBacklogSortOrderAsync(Guid projectId, CancellationToken cancellationToken);

    void AddTask(ProjectTask task);

    /// <summary>Reads a non-deleted task without tracking for read paths.</summary>
    Task<ProjectTask?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>Reads a non-deleted task for mutation paths; mutations are persisted by re-attaching through <see cref="MarkForUpdate"/>.</summary>
    Task<ProjectTask?> FindTaskForUpdateAsync(Guid taskId, CancellationToken cancellationToken);

    Task<bool> HasActiveAssigneeAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken);

    /// <summary>Re-attaches a task as modified, restoring the client version as EF's original concurrency value.</summary>
    void MarkForUpdate(ProjectTask task, Guid expectedVersion);

    /// <summary>Persists pending changes; returns false when an optimistic-concurrency conflict is detected.</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProjectTaskResponse>> ListBoardTasksAsync(Guid projectId, CancellationToken cancellationToken);
}
