using DNTU.SkillBridge.Application.Milestones;
using DNTU.SkillBridge.Application.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

public interface IProjectRealtimeNotifier
{
    Task NotifyTaskCreatedAsync(Guid projectId, ProjectTaskResponse task, CancellationToken cancellationToken = default);
    Task NotifyTaskUpdatedAsync(Guid projectId, ProjectTaskResponse task, CancellationToken cancellationToken = default);
    Task NotifyTaskMovedAsync(Guid projectId, ProjectTaskResponse task, CancellationToken cancellationToken = default);
    Task NotifyTaskDeletedAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);
    Task NotifyCommentAddedAsync(Guid projectId, TaskCommentResponse comment, CancellationToken cancellationToken = default);
    Task NotifyCommentDeletedAsync(Guid projectId, Guid taskId, Guid commentId, CancellationToken cancellationToken = default);
    Task NotifyChecklistUpdatedAsync(Guid projectId, TaskChecklistItemResponse item, CancellationToken cancellationToken = default);
    Task NotifyMilestoneUpdatedAsync(Guid projectId, MilestoneResponse milestone, CancellationToken cancellationToken = default);
    Task NotifyProjectStatusChangedAsync(Guid projectId, string status, CancellationToken cancellationToken = default);
}
