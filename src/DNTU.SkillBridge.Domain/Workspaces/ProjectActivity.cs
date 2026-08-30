using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Workspaces;

/// <summary>Append-only, safe metadata audit feed for project workspace events.</summary>
public sealed class ProjectActivity : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public Guid? ActorUserId { get; private set; }
    public string? MetadataJson { get; private set; }

    private ProjectActivity() { }

    public ProjectActivity(Guid projectId, string eventType, Guid? actorUserId, string? metadataJson = null)
    {
        ProjectId = projectId;
        EventType = string.IsNullOrWhiteSpace(eventType)
            ? throw new ArgumentException("An activity event type is required.", nameof(eventType))
            : eventType.Trim().ToUpperInvariant();
        ActorUserId = actorUserId;
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim();
    }
}
