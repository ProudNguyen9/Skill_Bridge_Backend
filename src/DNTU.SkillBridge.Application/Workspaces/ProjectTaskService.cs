using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

public interface IProjectTaskService
{
    Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> CreateAsync(Guid userId, Guid projectId, CreateProjectTaskRequest request, CancellationToken cancellationToken);

    Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);

    Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> UpdateAsync(Guid userId, Guid taskId, UpdateProjectTaskRequest request, CancellationToken cancellationToken);

    Task<ProjectTaskOutcome> DeleteAsync(Guid userId, Guid taskId, Guid version, CancellationToken cancellationToken);

    Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> MoveAsync(Guid userId, Guid taskId, MoveProjectTaskRequest request, CancellationToken cancellationToken);

    Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> AssignAsync(Guid userId, Guid taskId, AssignProjectTaskRequest request, CancellationToken cancellationToken);

    Task<KanbanBoardResponse?> BoardAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);
}

public sealed class ProjectTaskService(IProjectTaskRepository taskRepository, IProjectActivityWriter activityWriter, IUnitOfWork unitOfWork) : IProjectTaskService
{
    public async Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> CreateAsync(Guid userId, Guid projectId, CreateProjectTaskRequest request, CancellationToken cancellationToken)
    {
        if (!await taskRepository.IsMemberAsync(userId, projectId, cancellationToken)) return (ProjectTaskOutcome.Forbidden, null);
        var nextOrder = await taskRepository.GetMaxBacklogSortOrderAsync(projectId, cancellationToken) ?? -1;
        var task = new ProjectTask(projectId, request.Title, request.Description, request.Priority, request.DueAt, nextOrder + 1);
        taskRepository.AddTask(task);
        activityWriter.Append(projectId, "TASK_CREATED", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (ProjectTaskOutcome.Success, Map(task));
    }

    public async Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await taskRepository.FindTaskAsync(taskId, cancellationToken);
        if (task is null) return (ProjectTaskOutcome.NotFound, null);
        return await taskRepository.IsMemberAsync(userId, task.ProjectId, cancellationToken) ? (ProjectTaskOutcome.Success, Map(task)) : (ProjectTaskOutcome.Forbidden, null);
    }

    public async Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> UpdateAsync(Guid userId, Guid taskId, UpdateProjectTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await taskRepository.FindTaskForUpdateAsync(taskId, cancellationToken);
        if (task is null) return (ProjectTaskOutcome.NotFound, null);
        if (!await taskRepository.IsMemberAsync(userId, task.ProjectId, cancellationToken)) return (ProjectTaskOutcome.Forbidden, null);
        try { task.Update(request.Title, request.Description, request.Priority, request.DueAt, request.Version); }
        catch (InvalidOperationException) { return (ProjectTaskOutcome.Conflict, null); }
        taskRepository.MarkForUpdate(task, request.Version);
        activityWriter.Append(task.ProjectId, "TASK_UPDATED", userId);
        if (!await taskRepository.TrySaveChangesAsync(cancellationToken)) return (ProjectTaskOutcome.Conflict, null);
        return (ProjectTaskOutcome.Success, Map(task));
    }

    public async Task<ProjectTaskOutcome> DeleteAsync(Guid userId, Guid taskId, Guid version, CancellationToken cancellationToken)
    {
        var task = await taskRepository.FindTaskForUpdateAsync(taskId, cancellationToken);
        if (task is null) return ProjectTaskOutcome.NotFound;
        if (!await taskRepository.IsMemberAsync(userId, task.ProjectId, cancellationToken)) return ProjectTaskOutcome.Forbidden;
        try { task.Delete(version); }
        catch (InvalidOperationException) { return ProjectTaskOutcome.Conflict; }
        taskRepository.MarkForUpdate(task, version);
        activityWriter.Append(task.ProjectId, "TASK_DELETED", userId);
        if (!await taskRepository.TrySaveChangesAsync(cancellationToken)) return ProjectTaskOutcome.Conflict;
        return ProjectTaskOutcome.Success;
    }

    public async Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> MoveAsync(Guid userId, Guid taskId, MoveProjectTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await taskRepository.FindTaskForUpdateAsync(taskId, cancellationToken);
        if (task is null) return (ProjectTaskOutcome.NotFound, null);
        if (!await taskRepository.IsMemberAsync(userId, task.ProjectId, cancellationToken)) return (ProjectTaskOutcome.Forbidden, null);
        try { task.Move(request.Status, request.SortOrder, request.Version); }
        catch (InvalidOperationException) { return (ProjectTaskOutcome.Conflict, null); }
        taskRepository.MarkForUpdate(task, request.Version);
        activityWriter.Append(task.ProjectId, "TASK_MOVED", userId);
        if (!await taskRepository.TrySaveChangesAsync(cancellationToken)) return (ProjectTaskOutcome.Conflict, null);
        return (ProjectTaskOutcome.Success, Map(task));
    }

    public async Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> AssignAsync(Guid userId, Guid taskId, AssignProjectTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await taskRepository.FindTaskForUpdateAsync(taskId, cancellationToken);
        if (task is null) return (ProjectTaskOutcome.NotFound, null);
        if (!await taskRepository.IsMemberAsync(userId, task.ProjectId, cancellationToken)) return (ProjectTaskOutcome.Forbidden, null);
        if (request.StudentId.HasValue && !await taskRepository.HasActiveAssigneeAsync(task.ProjectId, request.StudentId.Value, cancellationToken)) return (ProjectTaskOutcome.InvalidAssignee, null);
        try { task.Assign(request.StudentId, request.Version); }
        catch (InvalidOperationException) { return (ProjectTaskOutcome.Conflict, null); }
        taskRepository.MarkForUpdate(task, request.Version);
        activityWriter.Append(task.ProjectId, "TASK_ASSIGNED", userId);
        if (!await taskRepository.TrySaveChangesAsync(cancellationToken)) return (ProjectTaskOutcome.Conflict, null);
        return (ProjectTaskOutcome.Success, Map(task));
    }

    public async Task<KanbanBoardResponse?> BoardAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!await taskRepository.IsMemberAsync(userId, projectId, cancellationToken)) return null;
        var tasks = await taskRepository.ListBoardTasksAsync(projectId, cancellationToken);
        var columns = Enum.GetValues<ProjectTaskStatus>().ToDictionary(status => status, status => (IReadOnlyCollection<ProjectTaskResponse>)tasks.Where(task => task.Status == status).ToList());
        return new KanbanBoardResponse(projectId, columns);
    }

    private static ProjectTaskResponse Map(ProjectTask task) => new(task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority, task.AssigneeStudentId, task.DueAt, task.SortOrder, task.Version, task.CreatedAt);
}
