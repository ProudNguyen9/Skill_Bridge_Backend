using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Projects;

/// <summary>Immutable completion marker; only one completion exists for a project.</summary>
public sealed class ProjectCompletion : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public Guid CompletedByUserId { get; private set; }
    public string EvidenceJson { get; private set; } = string.Empty;

    private ProjectCompletion() { }

    public ProjectCompletion(Guid projectId, Guid completedByUserId, string evidenceJson)
    {
        if (string.IsNullOrWhiteSpace(evidenceJson)) throw new ArgumentException("Completion evidence is required.", nameof(evidenceJson));
        ProjectId = projectId;
        CompletedByUserId = completedByUserId;
        EvidenceJson = evidenceJson;
    }
}
