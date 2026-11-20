using DNTU.SkillBridge.Domain.Meetings;
using Xunit;

namespace DNTU.SkillBridge.UnitTests;

public sealed class MeetingEntityTests
{
    [Fact]
    public void Constructor_NormalizesApprovedGoogleMeetUrl_AndSchedulesReminder()
    {
        var startAt = DateTimeOffset.UtcNow.AddDays(1);
        var meeting = new ProjectMeeting(Guid.NewGuid(), "  Sprint planning  ", "  Scope next sprint.  ", startAt, startAt.AddHours(1), "https://meet.google.com/abc-defg-hij");

        Assert.Equal("Sprint planning", meeting.Title);
        Assert.Equal("Scope next sprint.", meeting.Description);
        Assert.Equal(MeetingExternalLinkType.GOOGLE_MEET, meeting.ExternalLinkType);
        Assert.Equal(MeetingReminderState.SCHEDULED, meeting.ReminderState);
    }

    [Theory]
    [InlineData("http://meet.google.com/abc-defg-hij")]
    [InlineData("https://example.com/meeting")]
    [InlineData("https://evilzoom.example/meeting")]
    public void Constructor_RejectsUnsafeOrUnsupportedConferenceUrl(string url)
    {
        var startAt = DateTimeOffset.UtcNow.AddDays(1);

        Assert.Throws<ArgumentException>(() => new ProjectMeeting(Guid.NewGuid(), "Planning", null, startAt, startAt.AddHours(1), url));
    }

    [Fact]
    public void Constructor_RejectsNonUtcMeetingRange()
    {
        var localStart = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.FromHours(7));

        Assert.Throws<ArgumentException>(() => new ProjectMeeting(Guid.NewGuid(), "Planning", null, localStart, localStart.AddHours(1), null));
    }

    [Fact]
    public void InterviewConstructor_MarksMeetingTypeAndLinksApplication()
    {
        var startAt = DateTimeOffset.UtcNow.AddDays(1);
        var meeting = new ProjectMeeting(Guid.NewGuid(), Guid.NewGuid(), "Phỏng vấn: Đề tài X", null, startAt, startAt.AddMinutes(30), "https://meet.google.com/abc-defg-hij");

        Assert.Equal(MeetingType.INTERVIEW, meeting.MeetingType);
        Assert.NotNull(meeting.ApplicationId);
        Assert.Equal(MeetingExternalLinkType.GOOGLE_MEET, meeting.ExternalLinkType);
        Assert.Equal(MeetingReminderState.SCHEDULED, meeting.ReminderState);
    }

    [Fact]
    public void ProjectConstructor_DefaultsToProjectMeetingType()
    {
        var startAt = DateTimeOffset.UtcNow.AddDays(1);
        var meeting = new ProjectMeeting(Guid.NewGuid(), "Sprint planning", null, startAt, startAt.AddHours(1), null);

        Assert.Equal(MeetingType.PROJECT, meeting.MeetingType);
        Assert.Null(meeting.ApplicationId);
    }
}
