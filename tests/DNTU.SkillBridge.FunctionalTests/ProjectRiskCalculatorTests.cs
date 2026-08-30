using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.FunctionalTests;

public sealed class ProjectRiskCalculatorTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 0, ProjectRiskLevel.LOW)]
    [InlineData(0, 1, 0, 0, 1, ProjectRiskLevel.LOW)]
    [InlineData(1, 0, 0, 0, 2, ProjectRiskLevel.MEDIUM)]
    [InlineData(0, 3, 0, 0, 3, ProjectRiskLevel.MEDIUM)]
    [InlineData(0, 2, 0, 0, 2, ProjectRiskLevel.MEDIUM)]
    [InlineData(0, 0, 2, 0, 2, ProjectRiskLevel.MEDIUM)]
    [InlineData(2, 0, 0, 0, 4, ProjectRiskLevel.HIGH)]
    [InlineData(1, 1, 2, 1, 6, ProjectRiskLevel.HIGH)]
    public void Calculate_returns_expected_score_and_level(
        int overdueMilestones,
        int overdueTasks,
        int submissionRevisions,
        int missedMeetings,
        int expectedScore,
        ProjectRiskLevel expectedLevel)
    {
        var result = ProjectRiskCalculator.Calculate(overdueMilestones, overdueTasks, submissionRevisions, missedMeetings);

        Assert.Equal(expectedScore, result.Score);
        Assert.Equal(expectedLevel, result.Level);
    }

    [Fact]
    public void Calculate_emits_explainable_reasons_for_each_triggered_rule()
    {
        var result = ProjectRiskCalculator.Calculate(1, 1, 2, 1);

        Assert.Equal(4, result.Reasons.Count);
        Assert.All(result.Reasons, reason => Assert.Contains("(+", reason));
    }
}
