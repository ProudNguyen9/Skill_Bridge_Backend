using System.Text.Json;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;

namespace DNTU.SkillBridge.Api.Workspaces;

/// <summary>
/// Appends sanitized workspace activity inside the caller's existing EF Core transaction.
/// It intentionally exposes no update or delete operation.
/// </summary>
public interface IProjectActivityWriter
{
    void Append(Guid projectId, string eventType, Guid? actorUserId, object? metadata = null);
}

/// <summary>EF Core-backed append-only activity writer for workspace feature services.</summary>
public sealed class ProjectActivityWriter(AppDbContext dbContext) : IProjectActivityWriter
{
    public void Append(Guid projectId, string eventType, Guid? actorUserId, object? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        dbContext.ProjectActivities.Add(new ProjectActivity(
            projectId,
            eventType.Trim().ToUpperInvariant(),
            actorUserId,
            metadata is null ? null : JsonSerializer.Serialize(metadata)));
    }
}
