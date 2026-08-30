namespace DNTU.SkillBridge.Application.Workspaces;

/// <summary>
/// Appends sanitized workspace activity inside the caller's existing EF Core transaction.
/// It intentionally exposes no update or delete operation.
/// </summary>
public interface IProjectActivityWriter
{
    void Append(Guid projectId, string eventType, Guid? actorUserId, object? metadata = null);
}
