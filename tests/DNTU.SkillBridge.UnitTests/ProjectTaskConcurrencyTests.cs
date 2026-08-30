using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.UnitTests;

public sealed class ProjectTaskConcurrencyTests
{
    [Fact]
    public void Move_with_stale_version_is_rejected_without_mutating_the_task()
    {
        var task = new ProjectTask(
            Guid.NewGuid(),
            "Hoàn thành sơ đồ dữ liệu",
            null,
            ProjectTaskPriority.MEDIUM,
            null,
            0);
        var initialVersion = task.Version;

        task.Move(ProjectTaskStatus.IN_PROGRESS, 1, initialVersion);

        Assert.Equal(ProjectTaskStatus.IN_PROGRESS, task.Status);
        Assert.Throws<InvalidOperationException>(() =>
            task.Move(ProjectTaskStatus.DONE, 2, initialVersion));
        Assert.Equal(ProjectTaskStatus.IN_PROGRESS, task.Status);
    }
}
