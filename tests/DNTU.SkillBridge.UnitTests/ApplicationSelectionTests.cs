using DNTU.SkillBridge.Domain.Applications;
using ProjectApplication = DNTU.SkillBridge.Domain.Applications.Application;

namespace DNTU.SkillBridge.UnitTests;

public sealed class ApplicationSelectionTests
{
    [Fact]
    public void Accept_records_the_company_decision_audit_fields()
    {
        var application = new ProjectApplication(Guid.NewGuid(), Guid.NewGuid(), "Tôi có thể tham gia toàn thời gian.");
        var actorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        application.Accept(actorId, now, "Phù hợp với yêu cầu kỹ thuật.");

        Assert.Equal(ApplicationStatus.ACCEPTED, application.Status);
        Assert.Equal(actorId, application.DecidedByUserId);
        Assert.Equal(now, application.DecidedAt);
        Assert.Equal("Phù hợp với yêu cầu kỹ thuật.", application.DecisionReason);
    }

    [Fact]
    public void Withdrawn_application_cannot_be_accepted()
    {
        var application = new ProjectApplication(Guid.NewGuid(), Guid.NewGuid(), null);
        application.Withdraw(DateTimeOffset.UtcNow, "Đổi lịch học.");

        Assert.Throws<InvalidOperationException>(() =>
            application.Accept(Guid.NewGuid(), DateTimeOffset.UtcNow, null));
    }
}
