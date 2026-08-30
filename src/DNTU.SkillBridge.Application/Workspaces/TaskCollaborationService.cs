using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

public interface ITaskCollaborationService
{
    Task<TaskCommentResponse?> AddCommentAsync(ICurrentUser currentUser, Guid taskId, CreateTaskCommentRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TaskCommentResponse>?> ListCommentsAsync(ICurrentUser currentUser, Guid taskId, CancellationToken cancellationToken);

    Task<bool> DeleteCommentAsync(ICurrentUser currentUser, Guid taskId, Guid commentId, CancellationToken cancellationToken);

    Task<(TaskCollaborationOutcome Outcome, TaskChecklistItemResponse? Item)> AddChecklistItemAsync(ICurrentUser currentUser, Guid taskId, CreateChecklistItemRequest request, CancellationToken cancellationToken);

    Task<(TaskCollaborationOutcome Outcome, TaskChecklistItemResponse? Item)> UpdateChecklistItemAsync(ICurrentUser currentUser, Guid itemId, UpdateChecklistItemRequest request, CancellationToken cancellationToken);

    Task<TaskCollaborationOutcome> DeleteChecklistItemAsync(ICurrentUser currentUser, Guid itemId, Guid version, CancellationToken cancellationToken);

    Task<PagedResponse<ProjectTaskResponse>?> GetMyStudentTasksAsync(ICurrentUser currentUser, TaskListQuery query, CancellationToken cancellationToken);

    Task<PagedResponse<ProjectTaskResponse>?> GetLecturerTasksAsync(ICurrentUser currentUser, TaskListQuery query, CancellationToken cancellationToken);
}

public sealed class TaskCollaborationService(ITaskCollaborationRepository collaborationRepository, IProjectAccessService projectAccessService, IProjectActivityWriter activityWriter, IUnitOfWork unitOfWork)
    : ITaskCollaborationService
{
    public async Task<TaskCommentResponse?> AddCommentAsync(ICurrentUser currentUser, Guid taskId, CreateTaskCommentRequest request, CancellationToken cancellationToken)
    {
        var task = await AccessibleTaskAsync(currentUser, taskId, cancellationToken);
        if (task is null || currentUser.UserId is not Guid userId) return null;
        var comment = new TaskComment(task.Id, userId, request.Content);
        collaborationRepository.AddComment(comment);
        activityWriter.Append(task.ProjectId, "TASK_COMMENT_CREATED", userId, new { taskId = task.Id, commentId = comment.Id });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new TaskCommentResponse(comment.Id, comment.ProjectTaskId, comment.AuthorUserId, comment.Content, comment.CreatedAt);
    }

    public async Task<IReadOnlyCollection<TaskCommentResponse>?> ListCommentsAsync(ICurrentUser currentUser, Guid taskId, CancellationToken cancellationToken)
    {
        if (await AccessibleTaskAsync(currentUser, taskId, cancellationToken) is null) return null;
        return await collaborationRepository.ListCommentsAsync(taskId, cancellationToken);
    }

    public async Task<bool> DeleteCommentAsync(ICurrentUser currentUser, Guid taskId, Guid commentId, CancellationToken cancellationToken)
    {
        var task = await AccessibleTaskAsync(currentUser, taskId, cancellationToken);
        if (task is null || currentUser.UserId is not Guid userId) return false;
        var comment = await collaborationRepository.FindCommentAsync(commentId, taskId, cancellationToken);
        if (comment is null || (comment.AuthorUserId != userId && !await CanManageAsync(currentUser, task.ProjectId, cancellationToken))) return false;
        collaborationRepository.RemoveComment(comment);
        activityWriter.Append(task.ProjectId, "TASK_COMMENT_DELETED", userId, new { taskId = task.Id, commentId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(TaskCollaborationOutcome Outcome, TaskChecklistItemResponse? Item)> AddChecklistItemAsync(ICurrentUser currentUser, Guid taskId, CreateChecklistItemRequest request, CancellationToken cancellationToken)
    {
        var task = await AccessibleTaskAsync(currentUser, taskId, cancellationToken);
        if (task is null) return (TaskCollaborationOutcome.NotFound, null);
        if (currentUser.UserId is not Guid userId) return (TaskCollaborationOutcome.Forbidden, null);
        try { task.UpdateChecklistVersion(request.Version); }
        catch (InvalidOperationException) { return (TaskCollaborationOutcome.Conflict, null); }

        var order = await collaborationRepository.GetMaxChecklistSortOrderAsync(taskId, cancellationToken) ?? -1;
        var item = new TaskChecklistItem(taskId, request.Title, order + 1);
        collaborationRepository.AddChecklistItem(item);
        collaborationRepository.MarkTaskForChecklistUpdate(task, request.Version);
        activityWriter.Append(task.ProjectId, "TASK_CHECKLIST_CREATED", userId, new { taskId = task.Id, checklistItemId = item.Id });
        if (!await collaborationRepository.TrySaveChangesAsync(cancellationToken)) return (TaskCollaborationOutcome.Conflict, null);

        return (TaskCollaborationOutcome.Success, Map(item));
    }

    public async Task<(TaskCollaborationOutcome Outcome, TaskChecklistItemResponse? Item)> UpdateChecklistItemAsync(ICurrentUser currentUser, Guid itemId, UpdateChecklistItemRequest request, CancellationToken cancellationToken)
    {
        var item = await collaborationRepository.FindChecklistItemAsync(itemId, cancellationToken);
        if (item is null) return (TaskCollaborationOutcome.NotFound, null);
        var task = await AccessibleTaskAsync(currentUser, item.ProjectTaskId, cancellationToken);
        if (task is null) return (TaskCollaborationOutcome.NotFound, null);
        if (currentUser.UserId is not Guid userId) return (TaskCollaborationOutcome.Forbidden, null);
        try
        {
            task.UpdateChecklistVersion(request.Version);
            item.Update(request.Title, request.IsCompleted);
        }
        catch (InvalidOperationException) { return (TaskCollaborationOutcome.Conflict, null); }

        collaborationRepository.MarkTaskForChecklistUpdate(task, request.Version);
        activityWriter.Append(task.ProjectId, "TASK_CHECKLIST_UPDATED", userId, new { taskId = task.Id, checklistItemId = item.Id });
        if (!await collaborationRepository.TrySaveChangesAsync(cancellationToken)) return (TaskCollaborationOutcome.Conflict, null);

        return (TaskCollaborationOutcome.Success, Map(item));
    }

    public async Task<TaskCollaborationOutcome> DeleteChecklistItemAsync(ICurrentUser currentUser, Guid itemId, Guid version, CancellationToken cancellationToken)
    {
        var item = await collaborationRepository.FindChecklistItemAsync(itemId, cancellationToken);
        if (item is null) return TaskCollaborationOutcome.NotFound;
        var task = await AccessibleTaskAsync(currentUser, item.ProjectTaskId, cancellationToken);
        if (task is null) return TaskCollaborationOutcome.NotFound;
        if (currentUser.UserId is not Guid userId) return TaskCollaborationOutcome.Forbidden;
        try { task.UpdateChecklistVersion(version); }
        catch (InvalidOperationException) { return TaskCollaborationOutcome.Conflict; }

        collaborationRepository.RemoveChecklistItem(item);
        collaborationRepository.MarkTaskForChecklistUpdate(task, version);
        activityWriter.Append(task.ProjectId, "TASK_CHECKLIST_DELETED", userId, new { taskId = task.Id, checklistItemId = item.Id });
        if (!await collaborationRepository.TrySaveChangesAsync(cancellationToken)) return TaskCollaborationOutcome.Conflict;

        return TaskCollaborationOutcome.Success;
    }

    public async Task<PagedResponse<ProjectTaskResponse>?> GetMyStudentTasksAsync(ICurrentUser currentUser, TaskListQuery query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.Roles.Contains(RoleNames.Student) || currentUser.UserId is not Guid userId) return null;
        var (totalCount, items) = await collaborationRepository.ListStudentTasksAsync(userId, query, cancellationToken);
        return new PagedResponse<ProjectTaskResponse>(items, PageMetadata.Create(query.Page, query.PageSize, totalCount));
    }

    public async Task<PagedResponse<ProjectTaskResponse>?> GetLecturerTasksAsync(ICurrentUser currentUser, TaskListQuery query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.Roles.Contains(RoleNames.Lecturer) || currentUser.UserId is not Guid userId) return null;
        var (totalCount, items) = await collaborationRepository.ListLecturerTasksAsync(userId, query, cancellationToken);
        return new PagedResponse<ProjectTaskResponse>(items, PageMetadata.Create(query.Page, query.PageSize, totalCount));
    }

    private async Task<ProjectTask?> AccessibleTaskAsync(ICurrentUser currentUser, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await collaborationRepository.FindTaskAsync(taskId, cancellationToken);
        if (task is null || !(await projectAccessService.GetAsync(currentUser, task.ProjectId, cancellationToken)).CanRead)
        {
            return null;
        }

        return task;
    }

    private async Task<bool> CanManageAsync(ICurrentUser currentUser, Guid projectId, CancellationToken cancellationToken)
    {
        if ((await projectAccessService.GetAsync(currentUser, projectId, cancellationToken)).CanManage) return true;
        return currentUser.UserId.HasValue && await collaborationRepository.HasCompanyManagerAsync(currentUser.UserId.Value, projectId, cancellationToken);
    }

    private static TaskChecklistItemResponse Map(TaskChecklistItem item) => new(item.Id, item.ProjectTaskId, item.Title, item.IsCompleted, item.SortOrder);
}
