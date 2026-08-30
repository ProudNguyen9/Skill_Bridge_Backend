using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

public sealed class CreateTaskCommentRequest
{
    [Required, StringLength(4000, MinimumLength = 1)] public string Content { get; init; } = string.Empty;
}

public class CreateChecklistItemRequest
{
    [Required, StringLength(300, MinimumLength = 1)] public string Title { get; init; } = string.Empty;
    [Required] public Guid Version { get; init; }
}

public sealed class UpdateChecklistItemRequest
{
    [Required, StringLength(300, MinimumLength = 1)] public string Title { get; init; } = string.Empty;
    [Required] public Guid Version { get; init; }
    public bool IsCompleted { get; init; }
}

public sealed class TaskListQuery : PageQuery
{
    public ProjectTaskStatus? Status { get; init; }
    public ProjectTaskPriority? Priority { get; init; }
    public bool AssignedOnly { get; init; }
    public bool Overdue { get; init; }
}

public sealed record TaskCommentResponse(Guid Id, Guid TaskId, Guid AuthorUserId, string Content, DateTimeOffset CreatedAt);
public sealed record TaskChecklistItemResponse(Guid Id, Guid TaskId, string Title, bool IsCompleted, int SortOrder);
