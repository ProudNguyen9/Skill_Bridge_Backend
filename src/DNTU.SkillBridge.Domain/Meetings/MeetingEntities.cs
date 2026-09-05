using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Meetings;

public sealed class ProjectMeeting : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset EndAt { get; private set; }
    public string? ExternalUrl { get; private set; }
    public MeetingExternalLinkType ExternalLinkType { get; private set; }
    public MeetingReminderState ReminderState { get; private set; }

    private ProjectMeeting() { }

    public ProjectMeeting(Guid projectId, string title, string? description, DateTimeOffset startAt, DateTimeOffset endAt, string? externalUrl)
    {
        ProjectId = projectId;
        Update(title, description, startAt, endAt, externalUrl);
        ReminderState = MeetingReminderState.SCHEDULED;
    }

    public void Update(string title, string? description, DateTimeOffset startAt, DateTimeOffset endAt, string? externalUrl)
    {
        if (startAt.Offset != TimeSpan.Zero || endAt.Offset != TimeSpan.Zero) throw new ArgumentException("Meeting times must be provided in UTC.");
        if (endAt <= startAt) throw new ArgumentException("Meeting end time must be after start time.", nameof(endAt));
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("A meeting title is required.", nameof(title)) : title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ExternalUrl = NormalizeExternalUrl(externalUrl, out var linkType);
        ExternalLinkType = linkType;
        StartAt = startAt;
        EndAt = endAt;
        ReminderState = MeetingReminderState.SCHEDULED;
    }

    public void CancelReminder() => ReminderState = MeetingReminderState.CANCELLED;

    private static string? NormalizeExternalUrl(string? externalUrl, out MeetingExternalLinkType linkType)
    {
        if (string.IsNullOrWhiteSpace(externalUrl))
        {
            linkType = MeetingExternalLinkType.NONE;
            return null;
        }

        if (!Uri.TryCreate(externalUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("The meeting link must be a HTTPS Meet, Teams, or Zoom URL.", nameof(externalUrl));

        linkType = uri.Host.ToLowerInvariant() switch
        {
            "meet.google.com" => MeetingExternalLinkType.GOOGLE_MEET,
            "teams.microsoft.com" or "teams.live.com" => MeetingExternalLinkType.MICROSOFT_TEAMS,
            "zoom.us" or "www.zoom.us" => MeetingExternalLinkType.ZOOM,
            _ => throw new ArgumentException("The meeting link host must be Google Meet, Microsoft Teams, or Zoom.", nameof(externalUrl))
        };
        return uri.AbsoluteUri;
    }
}

public sealed class MeetingParticipant : AuditableEntity
{
    public Guid MeetingId { get; private set; }
    public Guid StudentId { get; private set; }
    public MeetingAttendanceStatus AttendanceStatus { get; private set; } = MeetingAttendanceStatus.PENDING;

    private MeetingParticipant() { }
    public MeetingParticipant(Guid meetingId, Guid studentId) => (MeetingId, StudentId) = (meetingId, studentId);
    public void SetAttendance(MeetingAttendanceStatus status) => AttendanceStatus = status;
}
