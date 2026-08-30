using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Workspaces;

/// <summary>
/// Resolves the current caller's effective access to a project workspace from persisted
/// membership, company ownership, lecturer assignment, and administrator roles.
/// This service is the authorization source of truth for workspace features.
/// </summary>
public sealed class ProjectAccessService(AppDbContext dbContext)
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
            var hasActiveMembership = await dbContext.ProjectMembers.AsNoTracking()
                .AnyAsync(member => member.ProjectId == projectId
                    && member.IsActive
                    && member.Student.UserId == userId,
                    cancellationToken);

            return hasActiveMembership ? ProjectAccess.Student : ProjectAccess.None;
        }

        if (currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin))
        {
            return ProjectAccess.Admin;
        }

        var hasCompanyAccess = await dbContext.Projects.AsNoTracking()
            .AnyAsync(project => project.Id == projectId
                && project.Company.Members.Any(member => member.UserId == userId),
                cancellationToken);
        if (hasCompanyAccess)
        {
            return ProjectAccess.Company;
        }

        var hasLecturerAccess = await dbContext.LecturerAssignments.AsNoTracking()
            .Join(
                dbContext.LecturerProfiles.AsNoTracking(),
                assignment => assignment.LecturerId,
                lecturer => lecturer.Id,
                (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(item => item.assignment.ProjectId == projectId
                && item.assignment.Status == LecturerAssignmentStatus.ACTIVE
                && item.lecturer.UserId == userId,
                cancellationToken);

        return hasLecturerAccess ? ProjectAccess.Lecturer : ProjectAccess.None;
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
