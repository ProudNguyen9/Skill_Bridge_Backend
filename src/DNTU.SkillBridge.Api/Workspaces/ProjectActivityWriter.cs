using System.Text.Json;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;

namespace DNTU.SkillBridge.Api.Workspaces;

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
