namespace DNTU.SkillBridge.Domain.Meetings;

public enum MeetingAttendanceStatus
{
    PENDING = 1,
    ATTENDED = 2,
    ABSENT = 3,
    EXCUSED = 4
}

public enum MeetingExternalLinkType
{
    NONE = 1,
    GOOGLE_MEET = 2,
    MICROSOFT_TEAMS = 3,
    ZOOM = 4
}

public enum MeetingReminderState
{
    NOT_SCHEDULED = 1,
    SCHEDULED = 2,
    CANCELLED = 3
}
