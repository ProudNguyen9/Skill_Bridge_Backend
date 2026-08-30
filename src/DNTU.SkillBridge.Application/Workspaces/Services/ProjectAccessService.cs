using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;

namespace DNTU.SkillBridge.Application.Workspaces;

/// <summary>
/// Resolves the current caller's effective access to a project workspace from persisted
/// membership, company ownership, lecturer assignment, and administrator roles.
/// This service is the authorization source of truth for workspace features.
/// </summary>
public sealed class ProjectAccessService(IProjectAccessRepository projectAccessRepository) : IProjectAccessService
{
    public async Task<ProjectAccess> GetAsync(
        ICurrentUser currentUser,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId)
        {
            return ProjectAccess.None;
        }

        if (currentUser.Roles.Contains(RoleNames.Student))
        {
            var hasActiveMembership = await projectAccessRepository.HasActiveStudentMembershipAsync(userId, projectId, cancellationToken);
            return hasActiveMembership ? ProjectAccess.Student : ProjectAccess.None;
        }

        if (currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin))
        {
            return ProjectAccess.Admin;
        }

        if (await projectAccessRepository.HasCompanyAccessAsync(userId, projectId, cancellationToken))
        {
            return ProjectAccess.Company;
        }

        return await projectAccessRepository.HasLecturerAccessAsync(userId, projectId, cancellationToken)
            ? ProjectAccess.Lecturer
            : ProjectAccess.None;
    }
}

/// <summary>Capability projection consumed by workspace read and mutation features.</summary>
public sealed record ProjectAccess(bool CanRead, bool CanManage, IReadOnlySet<string> Capabilities)
{
    public static ProjectAccess None { get; } = new(false, false, new HashSet<string>());
    public static ProjectAccess Student { get; } = new(true, false, new HashSet<string>(["workspace.read"]));
    public static ProjectAccess Lecturer { get; } = new(true, true, new HashSet<string>(["workspace.read", "workspace.manage", "academic.supervise"]));
    public static ProjectAccess Company { get; } = new(true, true, new HashSet<string>(["workspace.read", "workspace.manage"]));
    public static ProjectAccess Admin { get; } = new(true, true, new HashSet<string>(["workspace.read", "workspace.manage", "workspace.oversight"]));
}
