using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Workspaces;

/// <summary>Server-derived current risk state. Client input never supplies scores, levels, or reasons.</summary>
public sealed class ProjectRiskSnapshot : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public int Score { get; private set; }
    public ProjectRiskLevel Level { get; private set; }
    public string ReasonsJson { get; private set; } = "[]";
    public DateTimeOffset CalculatedAt { get; private set; }

    private ProjectRiskSnapshot() { }

    public ProjectRiskSnapshot(Guid projectId, int score, ProjectRiskLevel level, string reasonsJson, DateTimeOffset calculatedAt)
    {
        ProjectId = projectId;
        Apply(score, level, reasonsJson, calculatedAt);
    }

    public void Apply(int score, ProjectRiskLevel level, string reasonsJson, DateTimeOffset calculatedAt)
    {
        if (score < 0) throw new ArgumentOutOfRangeException(nameof(score));
        if (string.IsNullOrWhiteSpace(reasonsJson)) throw new ArgumentException("Risk reasons are required.", nameof(reasonsJson));
        Score = score;
        Level = level;
        ReasonsJson = reasonsJson;
        CalculatedAt = calculatedAt.ToUniversalTime();
    }
}

/// <summary>Append-only audit history for each server-side risk recalculation.</summary>
public sealed class ProjectRiskHistory : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public int Score { get; private set; }
    public ProjectRiskLevel Level { get; private set; }
    public string ReasonsJson { get; private set; } = "[]";
    public DateTimeOffset CalculatedAt { get; private set; }

    private ProjectRiskHistory() { }

    public ProjectRiskHistory(Guid projectId, int score, ProjectRiskLevel level, string reasonsJson, DateTimeOffset calculatedAt)
    {
        ProjectId = projectId;
        Score = score;
        Level = level;
        ReasonsJson = reasonsJson;
        CalculatedAt = calculatedAt.ToUniversalTime();
    }
}
