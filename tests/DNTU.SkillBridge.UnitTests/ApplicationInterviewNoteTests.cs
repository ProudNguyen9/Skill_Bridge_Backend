using System;
using DNTU.SkillBridge.Domain.Applications;
using Xunit;

namespace DNTU.SkillBridge.UnitTests;

public sealed class ApplicationInterviewNoteTests
{
    private static DNTU.SkillBridge.Domain.Applications.Application NewApplication() => new(Guid.NewGuid(), Guid.NewGuid(), null);

    [Fact]
    public void SetInterviewScheduleNote_AllowsPendingAndShortlisted()
    {
        var application = NewApplication();
        application.SetInterviewScheduleNote("Phỏng vấn lúc 14:00");

        application.Shortlist(Guid.NewGuid(), DateTimeOffset.UtcNow, null, null);
        application.SetInterviewScheduleNote("Phỏng vấn lúc 15:30");

        Assert.Equal(ApplicationStatus.SHORTLISTED, application.Status);
        Assert.Equal("Phỏng vấn lúc 15:30", application.InterviewScheduleNote);
    }

    [Fact]
    public void SetInterviewScheduleNote_BlankKeepsExistingValue()
    {
        var application = NewApplication();
        application.SetInterviewScheduleNote("Phỏng vấn lúc 14:00");

        application.SetInterviewScheduleNote("   ");

        Assert.Equal("Phỏng vấn lúc 14:00", application.InterviewScheduleNote);
    }

    [Fact]
    public void SetInterviewScheduleNote_RejectsFinalStatuses()
    {
        var application = NewApplication();
        application.Reject(Guid.NewGuid(), DateTimeOffset.UtcNow, "not a fit");

        Assert.Throws<InvalidOperationException>(() => application.SetInterviewScheduleNote("Phỏng vấn lúc 14:00"));
    }
}
