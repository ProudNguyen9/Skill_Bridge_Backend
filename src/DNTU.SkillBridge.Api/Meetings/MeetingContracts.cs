using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Meetings;

namespace DNTU.SkillBridge.Api.Meetings;

public sealed class CreateMeetingRequest
{
    [Required, StringLength(300, MinimumLength = 2)] public string Title { get; init; } = string.Empty;
    [StringLength(4000)] public string? Description { get; init; }
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    [Url, StringLength(1000)] public string? ExternalUrl { get; init; }
    [MinLength(1)] public IReadOnlyCollection<Guid> ParticipantStudentIds { get; init; } = [];
}

public sealed class UpdateMeetingRequest
{
    [Required, StringLength(300, MinimumLength = 2)] public string Title { get; init; } = string.Empty;
    [StringLength(4000)] public string? Description { get; init; }
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    [Url, StringLength(1000)] public string? ExternalUrl { get; init; }
}

public sealed class UpdateAttendanceRequest
{
    [Required] public Guid StudentId { get; init; }
    [Required] public MeetingAttendanceStatus AttendanceStatus { get; init; }
}

public sealed record MeetingResponse(Guid Id, Guid ProjectId, string Title, string? Description, DateTimeOffset StartAt, DateTimeOffset EndAt, string? ExternalUrl, MeetingExternalLinkType ExternalLinkType, MeetingReminderState ReminderState, DateTimeOffset CreatedAt);
public sealed record MeetingParticipantResponse(Guid StudentId, MeetingAttendanceStatus AttendanceStatus, DateTimeOffset? AttendanceUpdatedAt);
public sealed record MeetingReminderJob(Guid MeetingId, Guid ProjectId, DateTimeOffset StartAt, IReadOnlyCollection<Guid> ParticipantStudentIds);
