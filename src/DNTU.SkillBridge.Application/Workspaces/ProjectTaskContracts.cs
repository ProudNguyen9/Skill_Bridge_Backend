using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

public enum ProjectTaskOutcome { Success, NotFound, Forbidden, Conflict, InvalidAssignee }

public class CreateProjectTaskRequest
{
    [Required, StringLength(300, MinimumLength = 2)]
    public string Title { get; init; } = string.Empty;
    [StringLength(4000)] public string? Description { get; init; }
    public ProjectTaskPriority Priority { get; init; } = ProjectTaskPriority.MEDIUM;
    public DateTimeOffset? DueAt { get; init; }
}

public sealed class UpdateProjectTaskRequest : CreateProjectTaskRequest
{
    [Required] public Guid Version { get; init; }
}

public sealed class MoveProjectTaskRequest
{
    [Required] public Guid Version { get; init; }
    [Required] public ProjectTaskStatus Status { get; init; }
    [Range(0, int.MaxValue)] public int SortOrder { get; init; }
}

public sealed class AssignProjectTaskRequest
{
    [Required] public Guid Version { get; init; }
    public Guid? StudentId { get; init; }
}

public sealed record ProjectTaskResponse(Guid Id, Guid ProjectId, string Title, string? Description, ProjectTaskStatus Status, ProjectTaskPriority Priority, Guid? AssigneeStudentId, DateTimeOffset? DueAt, int SortOrder, Guid Version, DateTimeOffset CreatedAt);
public sealed record KanbanBoardResponse(Guid ProjectId, IReadOnlyDictionary<ProjectTaskStatus, IReadOnlyCollection<ProjectTaskResponse>> Columns);
