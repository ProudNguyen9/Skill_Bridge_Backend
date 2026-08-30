using DNTU.SkillBridge.Domain.Common;
using DNTU.SkillBridge.Domain.Students;

namespace DNTU.SkillBridge.Domain.Portfolio;

public sealed class VerifiedSkill : AuditableEntity
{
    public Guid StudentId { get; private set; }
    public Guid SkillId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid VerifiedByUserId { get; private set; }
    public SkillLevel Level { get; private set; }
    public Guid? EvidenceSubmissionId { get; private set; }
    public Guid? EvidenceEvaluationId { get; private set; }
    public DateTimeOffset VerifiedAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private VerifiedSkill() { }
    public VerifiedSkill(Guid studentId, Guid skillId, Guid projectId, Guid verifiedByUserId, SkillLevel level, Guid? evidenceSubmissionId = null, Guid? evidenceEvaluationId = null)
    {
        StudentId = studentId;
        SkillId = skillId;
        ProjectId = projectId;
        VerifiedByUserId = verifiedByUserId;
        Level = level;
        EvidenceSubmissionId = evidenceSubmissionId;
        EvidenceEvaluationId = evidenceEvaluationId;
        VerifiedAt = DateTimeOffset.UtcNow;
    }

    public void Restore(Guid verifiedByUserId, SkillLevel level, Guid? evidenceSubmissionId, Guid? evidenceEvaluationId)
    {
        VerifiedByUserId = verifiedByUserId;
        Level = level;
        EvidenceSubmissionId = evidenceSubmissionId;
        EvidenceEvaluationId = evidenceEvaluationId;
        VerifiedAt = DateTimeOffset.UtcNow;
        IsRevoked = false;
        RevokedAt = null;
    }

    public void Revoke(DateTimeOffset at) { IsRevoked = true; RevokedAt = at; }
}

public sealed class PortfolioEntry : AuditableEntity
{
    public Guid StudentId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public bool IsPublished { get; private set; }

    private PortfolioEntry() { }
    public PortfolioEntry(Guid studentId, Guid projectId, string summary)
    {
        StudentId = studentId;
        ProjectId = projectId;
        Summary = string.IsNullOrWhiteSpace(summary) ? throw new ArgumentException("Portfolio summary is required.", nameof(summary)) : summary.Trim();
    }
    public void Update(string summary) => Summary = string.IsNullOrWhiteSpace(summary) ? throw new ArgumentException("Portfolio summary is required.", nameof(summary)) : summary.Trim();
    public void Publish() => IsPublished = true;
    public void Unpublish() => IsPublished = false;
}
