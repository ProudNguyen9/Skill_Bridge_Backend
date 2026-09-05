using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Projects;

public sealed class ProjectApproval : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public ProjectDecision Decision { get; private set; }
    public string? Note { get; private set; }
    public Guid DecidedByUserId { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }

    private ProjectApproval() { }

    public ProjectApproval(Guid projectId, ProjectDecision decision, Guid decidedByUserId, DateTimeOffset decidedAt, string? note)
    {
        ProjectId = projectId;
        Decision = decision;
        DecidedByUserId = decidedByUserId;
        DecidedAt = decidedAt;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
