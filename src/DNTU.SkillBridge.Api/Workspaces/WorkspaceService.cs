using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Workspaces;

public sealed class WorkspaceService(AppDbContext dbContext, ProjectAccessService projectAccessService)
{
    public async Task<WorkspaceOverviewResponse?> GetOverviewAsync(ICurrentUser currentUser, Guid projectId, CancellationToken cancellationToken)
    {
        var access = await projectAccessService.GetAsync(currentUser, projectId, cancellationToken);
        if (!access.CanRead) return null;

        var project = await dbContext.Projects.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new { item.Id, item.Title, item.Slug, item.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null) return null;

        var memberCount = await dbContext.ProjectMembers.CountAsync(item => item.ProjectId == projectId && item.IsActive, cancellationToken);
        var openTaskCount = await dbContext.ProjectTasks.CountAsync(item => item.ProjectId == projectId && !item.IsDeleted && item.Status != ProjectTaskStatus.DONE, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var overdueTaskCount = await dbContext.ProjectTasks.CountAsync(item => item.ProjectId == projectId && !item.IsDeleted && item.Status != ProjectTaskStatus.DONE && item.DueAt < now, cancellationToken);
        return new WorkspaceOverviewResponse(project.Id, project.Title, project.Slug, project.Status.ToString(), memberCount, openTaskCount, overdueTaskCount, access.Capabilities);
    }

    public async Task<IReadOnlyCollection<WorkspaceMemberResponse>?> GetTeamAsync(ICurrentUser currentUser, Guid projectId, CancellationToken cancellationToken)
    {
        if (!(await projectAccessService.GetAsync(currentUser, projectId, cancellationToken)).CanRead) return null;
        return await dbContext.ProjectMembers.AsNoTracking().Where(member => member.ProjectId == projectId && member.IsActive)
            .Join(dbContext.StudentProfiles.AsNoTracking(), member => member.StudentId, student => student.Id,
                (member, student) => new WorkspaceMemberResponse(member.StudentId, student.StudentCode ?? "Student", member.CreatedAt))
            .OrderBy(member => member.DisplayName).ToListAsync(cancellationToken);
    }

    public async Task<PagedResponse<WorkspaceActivityResponse>?> GetActivityAsync(ICurrentUser currentUser, Guid projectId, WorkspaceActivityQuery query, CancellationToken cancellationToken)
    {
        if (!(await projectAccessService.GetAsync(currentUser, projectId, cancellationToken)).CanRead) return null;
        var activityQuery = dbContext.ProjectActivities.AsNoTracking().Where(activity => activity.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            var eventType = query.EventType.Trim().ToUpperInvariant();
            activityQuery = activityQuery.Where(activity => activity.EventType == eventType);
        }

        var total = await activityQuery.CountAsync(cancellationToken);
        var items = await activityQuery.OrderByDescending(activity => activity.CreatedAt).ThenByDescending(activity => activity.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(activity => new WorkspaceActivityResponse(activity.Id, activity.EventType, activity.CreatedAt)).ToListAsync(cancellationToken);
        return new PagedResponse<WorkspaceActivityResponse>(items, PageMetadata.Create(query.Page, query.PageSize, total));
    }

    public async Task<IReadOnlyCollection<WorkspaceOverviewResponse>> ListMyProjectsAsync(ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId) return [];

        // Correlated counts preserve a single server-side projection rather than an N+1 overview loop.
        var now = DateTimeOffset.UtcNow;
        var rows = await dbContext.Projects.AsNoTracking()
            .Where(project => dbContext.ProjectMembers.Any(member => member.ProjectId == project.Id && member.IsActive && member.Student.UserId == userId))
            .OrderByDescending(project => project.UpdatedAt)
            .ThenByDescending(project => project.Id)
            .Select(project => new
            {
                project.Id,
                project.Title,
                project.Slug,
                project.Status,
                MemberCount = dbContext.ProjectMembers.Count(member => member.ProjectId == project.Id && member.IsActive),
                OpenTaskCount = dbContext.ProjectTasks.Count(task => task.ProjectId == project.Id && !task.IsDeleted && task.Status != ProjectTaskStatus.DONE),
                OverdueTaskCount = dbContext.ProjectTasks.Count(task => task.ProjectId == project.Id && !task.IsDeleted && task.Status != ProjectTaskStatus.DONE && task.DueAt < now)
            })
            .ToListAsync(cancellationToken);
        return rows.Select(project => new WorkspaceOverviewResponse(
            project.Id,
            project.Title,
            project.Slug,
            project.Status.ToString(),
            project.MemberCount,
            project.OpenTaskCount,
            project.OverdueTaskCount,
            new HashSet<string>(["workspace.read"])))
            .ToList();
    }

    public async Task<WorkspaceSettingsResponse?> GetSettingsAsync(ICurrentUser currentUser, Guid projectId, CancellationToken cancellationToken)
    {
        if (!(await projectAccessService.GetAsync(currentUser, projectId, cancellationToken)).CanRead) return null;
        var settings = await dbContext.WorkspaceSettings.AsNoTracking().SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);
        return settings is null ? new WorkspaceSettingsResponse(projectId, true, true, null, null) : MapSettings(settings);
    }

    public async Task<WorkspaceSettingsResponse?> UpdateSettingsAsync(ICurrentUser currentUser, Guid projectId, UpdateWorkspaceSettingsRequest request, CancellationToken cancellationToken)
    {
        var access = await projectAccessService.GetAsync(currentUser, projectId, cancellationToken);
        if (!access.CanManage) return null;
        var settings = await dbContext.WorkspaceSettings.SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);
        if (settings is null)
        {
            settings = new WorkspaceSettings(projectId);
            dbContext.WorkspaceSettings.Add(settings);
        }
        settings.Update(request.MembersCanCreateTasks, request.MembersCanScheduleMeetings, request.WorkingAgreement);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSettings(settings);
    }

    private static WorkspaceSettingsResponse MapSettings(WorkspaceSettings settings) => new(settings.ProjectId, settings.MembersCanCreateTasks, settings.MembersCanScheduleMeetings, settings.WorkingAgreement, settings.UpdatedAt);
}
