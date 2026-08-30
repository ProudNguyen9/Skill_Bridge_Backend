using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Api.Workspaces;
using DNTU.SkillBridge.Domain.Meetings;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Api.Meetings;

public sealed class UpsertMeetingMinutesRequest
{
    [Required, StringLength(10000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}

public class CreateMeetingActionItemRequest
{
    [Required, StringLength(2000, MinimumLength = 1)]
    public string Description { get; init; } = string.Empty;
    public Guid? ResponsibleStudentId { get; init; }
    public DateTimeOffset? DueAt { get; init; }
}

public sealed class UpdateMeetingActionItemRequest : CreateMeetingActionItemRequest
{
    [Required]
    public Guid Version { get; init; }
    public bool IsCompleted { get; init; }
}

/// <summary>Optional task details for the first conversion only; subsequent calls return the existing linked task.</summary>
public sealed class ConvertMeetingActionItemRequest
{
    [StringLength(300, MinimumLength = 2)]
    public string? Title { get; init; }
    [StringLength(4000)]
    public string? Description { get; init; }
    public ProjectTaskPriority Priority { get; init; } = ProjectTaskPriority.MEDIUM;
}

public sealed record MeetingMinutesResponse(Guid MeetingId, string Content, Guid UpdatedByUserId, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
public sealed record MeetingMinuteRevisionResponse(Guid Id, string Content, Guid EditedByUserId, DateTimeOffset CreatedAt);
public sealed record MeetingActionItemResponse(Guid Id, Guid MeetingId, string Description, Guid? ResponsibleStudentId, DateTimeOffset? DueAt, bool IsCompleted, Guid? ProjectTaskId, Guid Version, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
public sealed record MeetingActionItemConversionResponse(MeetingActionItemResponse ActionItem, ProjectTaskResponse Task, bool AlreadyConverted);
