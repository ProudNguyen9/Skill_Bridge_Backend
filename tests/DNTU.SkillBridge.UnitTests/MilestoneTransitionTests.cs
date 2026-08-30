using DNTU.SkillBridge.Domain.Milestones;

namespace DNTU.SkillBridge.UnitTests;

public sealed class MilestoneTransitionTests
{
    [Fact]
    public void Revision_required_milestone_can_be_started_again_and_submitted()
    {
        var milestone = new ProjectMilestone(Guid.NewGuid(), "Thiết kế dữ liệu", null, 1, DateTimeOffset.UtcNow.AddDays(7));

        milestone.Start(milestone.Version);
        milestone.Submit(milestone.Version);
        milestone.RequestRevision(milestone.Version);
        milestone.Start(milestone.Version);
        milestone.Submit(milestone.Version);

        Assert.Equal(MilestoneStatus.SUBMITTED, milestone.Status);
    }

    [Fact]
    public void Stale_approval_is_rejected_without_changing_the_milestone()
    {
        var milestone = new ProjectMilestone(Guid.NewGuid(), "Bàn giao", null, 1, DateTimeOffset.UtcNow.AddDays(7));
        milestone.Start(milestone.Version);
        milestone.Submit(milestone.Version);
        var staleVersion = milestone.Version;
        milestone.RequestRevision(milestone.Version);

        Assert.Throws<InvalidOperationException>(() => milestone.Approve(staleVersion));
        Assert.Equal(MilestoneStatus.REVISION_REQUIRED, milestone.Status);
    }

    [Fact]
    public void Approved_milestone_cannot_be_updated()
    {
        var milestone = new ProjectMilestone(Guid.NewGuid(), "Bàn giao", null, 1, DateTimeOffset.UtcNow.AddDays(7));
        milestone.Start(milestone.Version);
        milestone.Submit(milestone.Version);
        milestone.Approve(milestone.Version);

        Assert.Throws<InvalidOperationException>(() => milestone.Update("Đổi tên", null, 1, DateTimeOffset.UtcNow.AddDays(8), milestone.Version));
        Assert.Equal(MilestoneStatus.APPROVED, milestone.Status);
    }
}
