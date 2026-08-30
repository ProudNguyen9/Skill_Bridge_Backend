using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Projects;

/// <summary>
/// Append-only workflow decisions recorded against a project. The current state lives on
/// the Project entity itself; this table never gets rows updated or deleted so the full
/// decision history (who, when, what, why) is preserved.
/// </summary>
public enum ProjectDecision
{
    SUBMITTED = 1,
    APPROVED = 2,
    CHANGES_REQUESTED = 3,
    REJECTED = 4,
    SUSPENDED = 5,
    RESUMED = 6,
    CANCELLED = 7,
    REOPENED = 8
}

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
