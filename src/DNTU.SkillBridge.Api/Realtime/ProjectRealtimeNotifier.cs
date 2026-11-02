using DNTU.SkillBridge.Application.Milestones;
using DNTU.SkillBridge.Application.Workspaces;
using Microsoft.AspNetCore.SignalR;

namespace DNTU.SkillBridge.Api.Realtime;

public sealed class ProjectRealtimeNotifier(IHubContext<ProjectHub> hubContext) : IProjectRealtimeNotifier
{
    public Task NotifyTaskCreatedAsync(Guid projectId, ProjectTaskResponse task, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ProjectHub.ProjectGroup(projectId)).SendAsync("TaskCreated", task, cancellationToken);

    public Task NotifyTaskUpdatedAsync(Guid projectId, ProjectTaskResponse task, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ProjectHub.ProjectGroup(projectId)).SendAsync("TaskUpdated", task, cancellationToken);

    public Task NotifyTaskMovedAsync(Guid projectId, ProjectTaskResponse task, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ProjectHub.ProjectGroup(projectId)).SendAsync("TaskMoved", task, cancellationToken);

    public Task NotifyTaskDeletedAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ProjectHub.ProjectGroup(projectId)).SendAsync("TaskDeleted", new { taskId = taskId.ToString() }, cancellationToken);

    public Task NotifyCommentAddedAsync(Guid projectId, TaskCommentResponse comment, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ProjectHub.ProjectGroup(projectId)).SendAsync("TaskCommentAdded", new
        {
            id = comment.Id.ToString(),
            taskId = comment.TaskId.ToString(),
            authorUserId = comment.AuthorUserId.ToString(),
            content = comment.Content,
            createdAt = comment.CreatedAt
        }, cancellationToken);

    public Task NotifyCommentDeletedAsync(Guid projectId, Guid taskId, Guid commentId, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ProjectHub.ProjectGroup(projectId)).SendAsync("TaskCommentDeleted", new
        {
            taskId = taskId.ToString(),
            commentId = commentId.ToString()
        }, cancellationToken);

    public Task NotifyChecklistUpdatedAsync(Guid projectId, TaskChecklistItemResponse item, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ProjectHub.ProjectGroup(projectId)).SendAsync("ChecklistItemUpdated", new
        {
            id = item.Id.ToString(),
            taskId = item.TaskId.ToString(),
            title = item.Title,
            isCompleted = item.IsCompleted,
            sortOrder = item.SortOrder
        }, cancellationToken);

    public Task NotifyMilestoneUpdatedAsync(Guid projectId, MilestoneResponse milestone, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ProjectHub.ProjectGroup(projectId)).SendAsync("MilestoneUpdated", milestone, cancellationToken);

    public Task NotifyProjectStatusChangedAsync(Guid projectId, string status, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ProjectHub.ProjectGroup(projectId)).SendAsync("ProjectStatusChanged", new
        {
            projectId = projectId.ToString(),
            status
        }, cancellationToken);
}
