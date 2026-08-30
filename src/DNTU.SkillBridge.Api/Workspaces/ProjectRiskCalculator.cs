using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Api.Workspaces;

public sealed record ProjectRiskPolicy(
    int OverdueMilestonePoints = 2,
    int OverdueTaskOverThreeDaysPoints = 1,
    int RepeatedSubmissionRevisionPoints = 2,
    int MissedImportantMeetingPoints = 1,
    int RevisionThreshold = 2,
    int MediumRiskThreshold = 2,
    int HighRiskThreshold = 4);

public sealed record ProjectRiskAssessment(int Score, ProjectRiskLevel Level, IReadOnlyCollection<string> Reasons);

/// <summary>Pure deterministic server policy; callers supply only server-derived counts.</summary>
public static class ProjectRiskCalculator
{
    public static ProjectRiskAssessment Calculate(
        int overdueMilestones,
        int overdueTasksOverThreeDays,
        int submissionRevisionCount,
        int missedImportantMeetings,
        ProjectRiskPolicy? policy = null)
    {
        if (overdueMilestones < 0 || overdueTasksOverThreeDays < 0 || submissionRevisionCount < 0 || missedImportantMeetings < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(overdueMilestones), "Risk inputs must be non-negative.");
        }

        policy ??= new ProjectRiskPolicy();
        var reasons = new List<string>();
        var score = 0;
        if (overdueMilestones > 0)
        {
            score += overdueMilestones * policy.OverdueMilestonePoints;
            reasons.Add($"{overdueMilestones} overdue milestone(s) (+{overdueMilestones * policy.OverdueMilestonePoints})");
        }
        if (overdueTasksOverThreeDays > 0)
        {
            score += overdueTasksOverThreeDays * policy.OverdueTaskOverThreeDaysPoints;
            reasons.Add($"{overdueTasksOverThreeDays} task(s) overdue more than three days (+{overdueTasksOverThreeDays * policy.OverdueTaskOverThreeDaysPoints})");
        }
        if (submissionRevisionCount >= policy.RevisionThreshold)
        {
            score += policy.RepeatedSubmissionRevisionPoints;
            reasons.Add($"submission revision count is at least {policy.RevisionThreshold} (+{policy.RepeatedSubmissionRevisionPoints})");
        }
        if (missedImportantMeetings > 0)
        {
            score += missedImportantMeetings * policy.MissedImportantMeetingPoints;
            reasons.Add($"{missedImportantMeetings} missed important meeting(s) (+{missedImportantMeetings * policy.MissedImportantMeetingPoints})");
        }

        var level = score >= policy.HighRiskThreshold
            ? ProjectRiskLevel.HIGH
            : score >= policy.MediumRiskThreshold
                ? ProjectRiskLevel.MEDIUM
                : ProjectRiskLevel.LOW;
        return new ProjectRiskAssessment(score, level, reasons);
    }
}
