using System.Security.Claims;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Realtime;

[Authorize]
public sealed class ProjectHub(AppDbContext dbContext) : Hub
{
    public async Task JoinProject(Guid projectId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var id) || !await dbContext.ProjectMembers.AnyAsync(x => x.ProjectId == projectId && x.IsActive && x.Student.UserId == id, Context.ConnectionAborted)) throw new HubException("Project access is denied.");
        await Groups.AddToGroupAsync(Context.ConnectionId, ProjectGroup(projectId), Context.ConnectionAborted);
    }
    public static string ProjectGroup(Guid projectId) => $"project:{projectId:N}";
}
