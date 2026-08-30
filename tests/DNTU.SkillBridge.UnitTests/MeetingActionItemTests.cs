using DNTU.SkillBridge.Domain.Meetings;
using Xunit;

namespace DNTU.SkillBridge.UnitTests;

public sealed class MeetingActionItemTests
{
    [Fact]
    public void Update_RequiresCurrentVersion_AndRenewsIt()
    {
        var actionItem = new MeetingActionItem(Guid.NewGuid(), "Chuẩn bị bản demo", null, DateTimeOffset.UtcNow.AddDays(1));
        var originalVersion = actionItem.Version;

        actionItem.Update("Chuẩn bị bản demo đã rà soát", null, DateTimeOffset.UtcNow.AddDays(2), originalVersion);

        Assert.NotEqual(originalVersion, actionItem.Version);
        Assert.Throws<InvalidOperationException>(() => actionItem.SetCompleted(true, originalVersion));
    }

    [Fact]
    public void LinkTask_OnlyPermitsOneImmutableLinkedTask()
    {
        var actionItem = new MeetingActionItem(Guid.NewGuid(), "Phân tích yêu cầu", Guid.NewGuid(), null);
        var taskId = Guid.NewGuid();

        actionItem.LinkTask(taskId);

        Assert.Equal(taskId, actionItem.ProjectTaskId);
        Assert.Throws<InvalidOperationException>(() => actionItem.LinkTask(Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_RejectsBlankOrOversizedDescription()
    {
        Assert.Throws<ArgumentException>(() => new MeetingActionItem(Guid.NewGuid(), " ", null, null));
        Assert.Throws<ArgumentException>(() => new MeetingActionItem(Guid.NewGuid(), new string('x', 2001), null, null));
    }
}
