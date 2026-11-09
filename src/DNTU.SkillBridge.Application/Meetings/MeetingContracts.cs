using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Meetings;

namespace DNTU.SkillBridge.Application.Meetings;

public sealed class CreateMeetingRequest
{
    [Required, StringLength(300, MinimumLength = 2)] public string Title { get; init; } = string.Empty;
    [StringLength(4000)] public string? Description { get; init; }
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    public string? ExternalUrl { get; init; }
    public string? MeetingUrl { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
    public int? DurationMinutes { get; init; }
    public IReadOnlyCollection<Guid> ParticipantStudentIds { get; init; } = [];

    public DateTimeOffset ResolvedStartAt => (ScheduledAt ?? (StartAt != default ? StartAt : DateTimeOffset.UtcNow.AddHours(1))).ToUniversalTime();
    public DateTimeOffset ResolvedEndAt => (EndAt != default && EndAt > (ScheduledAt ?? StartAt))
        ? EndAt.ToUniversalTime()
        : ResolvedStartAt.AddMinutes(DurationMinutes ?? 60);
    public string? ResolvedUrl => !string.IsNullOrWhiteSpace(ExternalUrl) ? ExternalUrl : MeetingUrl;
}

public sealed class UpdateMeetingRequest
{
    [Required, StringLength(300, MinimumLength = 2)] public string Title { get; init; } = string.Empty;
    [StringLength(4000)] public string? Description { get; init; }
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    public string? ExternalUrl { get; init; }
    public string? MeetingUrl { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
    public int? DurationMinutes { get; init; }

    public DateTimeOffset ResolvedStartAt => (ScheduledAt ?? (StartAt != default ? StartAt : DateTimeOffset.UtcNow.AddHours(1))).ToUniversalTime();
    public DateTimeOffset ResolvedEndAt => (EndAt != default && EndAt > (ScheduledAt ?? StartAt))
        ? EndAt.ToUniversalTime()
        : ResolvedStartAt.AddMinutes(DurationMinutes ?? 60);
    public string? ResolvedUrl => !string.IsNullOrWhiteSpace(ExternalUrl) ? ExternalUrl : MeetingUrl;
}

public sealed class UpdateAttendanceRequest
{
    [Required] public Guid StudentId { get; init; }
    [Required] public MeetingAttendanceStatus AttendanceStatus { get; init; }
}

public sealed record MeetingResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? ExternalUrl,
    MeetingExternalLinkType ExternalLinkType,
    MeetingReminderState ReminderState,
    DateTimeOffset CreatedAt)
{
    public string? MeetingUrl => ExternalUrl;
    public DateTimeOffset ScheduledAt => StartAt;
    public int DurationMinutes => (int)(EndAt - StartAt).TotalMinutes;
    public bool IsCancelled => ReminderState == MeetingReminderState.CANCELLED;
}
public sealed record MeetingParticipantResponse(Guid StudentId, MeetingAttendanceStatus AttendanceStatus, DateTimeOffset? AttendanceUpdatedAt);
public sealed record MeetingReminderJob(Guid MeetingId, Guid ProjectId, DateTimeOffset StartAt, IReadOnlyCollection<Guid> ParticipantStudentIds);
