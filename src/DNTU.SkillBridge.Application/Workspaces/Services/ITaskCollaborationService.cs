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
