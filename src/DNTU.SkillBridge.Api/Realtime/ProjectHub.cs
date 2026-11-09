using System.Security.Claims;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DNTU.SkillBridge.Api.Realtime;

[Authorize]
public sealed class ProjectHub(IProjectAccessRepository projectAccessRepository) : Hub
{
    public async Task JoinProject(Guid projectId)
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new HubException("Project access is denied.");
        }

        var isAdmin = Context.User?.IsInRole(RoleNames.Admin) == true 
                   || Context.User?.IsInRole(RoleNames.SuperAdmin) == true;

        if (isAdmin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ProjectGroup(projectId), Context.ConnectionAborted);
            return;
        }

        var hasAccess = await projectAccessRepository.HasActiveStudentMembershipAsync(userId, projectId, Context.ConnectionAborted)
            || await projectAccessRepository.HasCompanyAccessAsync(userId, projectId, Context.ConnectionAborted)
            || await projectAccessRepository.HasLecturerAccessAsync(userId, projectId, Context.ConnectionAborted);

        if (!hasAccess)
        {
            throw new HubException("Project access is denied.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ProjectGroup(projectId), Context.ConnectionAborted);
    }

    public async Task LeaveProject(Guid projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectGroup(projectId), Context.ConnectionAborted);
    }

    public static string ProjectGroup(Guid projectId) => $"project:{projectId:N}";
}
