using DNTU.SkillBridge.Application.Common.Security;

namespace DNTU.SkillBridge.Application.Workspaces;

/// <summary>
/// Resolves the current caller's effective access to a project workspace from persisted
/// membership, company ownership, lecturer assignment, and administrator roles.
/// This service is the authorization source of truth for workspace features.
/// </summary>
public interface IProjectAccessService
{
    Task<ProjectAccess> GetAsync(
        ICurrentUser currentUser,
        Guid projectId,
        CancellationToken cancellationToken);
}
