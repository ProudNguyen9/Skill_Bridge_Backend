using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Academics;

public sealed class AcademicEvaluation : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid RubricId { get; private set; }
    public Guid EvaluatorUserId { get; private set; }
    public decimal TotalScore { get; private set; }
    public bool IsPassed { get; private set; }
    public AcademicEvaluationStatus Status { get; private set; } = AcademicEvaluationStatus.DRAFT;
    public DateTimeOffset? FinalizedAt { get; private set; }
    public Guid Version { get; private set; } = Guid.CreateVersion7();
    public ICollection<AcademicCriterionScore> Scores { get; } = [];

    private AcademicEvaluation() { }

    public AcademicEvaluation(Guid projectId, Guid studentId, Guid rubricId, Guid evaluatorUserId)
    {
        ProjectId = projectId;
        StudentId = studentId;
        RubricId = rubricId;
        EvaluatorUserId = evaluatorUserId;
    }

    public void ReplaceScores(IEnumerable<AcademicCriterionScore> scores, Guid? expectedVersion = null, decimal passScore = 50m)
    {
        if (expectedVersion.HasValue && expectedVersion.Value != Version) throw new InvalidOperationException("The evaluation was changed by another request.");
        if (Status == AcademicEvaluationStatus.FINALIZED) throw new InvalidOperationException("Finalized evaluations are immutable.");
        Scores.Clear();
        foreach (var score in scores) Scores.Add(score);
        TotalScore = Math.Round(Scores.Sum(score => score.WeightedScore), 2, MidpointRounding.AwayFromZero);
        IsPassed = TotalScore >= passScore;
        Status = AcademicEvaluationStatus.READY;
        Version = Guid.CreateVersion7();
    }

    public void FinalizeEvaluation(DateTimeOffset at)
    {
        if (Status != AcademicEvaluationStatus.READY) throw new InvalidOperationException("An evaluation must be ready before finalization.");
        Status = AcademicEvaluationStatus.FINALIZED;
        FinalizedAt = at;
        Version = Guid.CreateVersion7();
    }
}

public sealed class AcademicCriterionScore : AuditableEntity
{
    public Guid EvaluationId { get; private set; }
    public Guid RubricCriterionId { get; private set; }
    public decimal Score { get; private set; }
    public decimal Weight { get; private set; }
    public decimal WeightedScore => Score * Weight / 100m;

    private AcademicCriterionScore() { }
    public AcademicCriterionScore(Guid evaluationId, Guid rubricCriterionId, decimal score, decimal weight)
    {
        if (score is < 0 or > 100 || weight is <= 0 or > 100) throw new ArgumentOutOfRangeException(nameof(score));
        EvaluationId = evaluationId;
        RubricCriterionId = rubricCriterionId;
        Score = score;
        Weight = weight;
    }
}
