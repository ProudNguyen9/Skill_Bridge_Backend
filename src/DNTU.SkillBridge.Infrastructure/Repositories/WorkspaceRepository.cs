using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the workspace data operations.</summary>
public sealed class WorkspaceRepository(AppDbContext dbContext) : IWorkspaceRepository
{
    public Task<WorkspaceProjectRow?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new WorkspaceProjectRow(item.Id, item.Title, item.Slug, item.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountActiveMembersAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.CountAsync(item => item.ProjectId == projectId && item.IsActive, cancellationToken);

    public Task<int> CountOpenTasksAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectTasks.CountAsync(item => item.ProjectId == projectId && !item.IsDeleted && item.Status != ProjectTaskStatus.DONE, cancellationToken);

    public Task<int> CountOverdueTasksAsync(Guid projectId, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.ProjectTasks.CountAsync(item => item.ProjectId == projectId && !item.IsDeleted && item.Status != ProjectTaskStatus.DONE && item.DueAt < now, cancellationToken);

    public async Task<IReadOnlyCollection<WorkspaceMemberResponse>> ListTeamAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers.AsNoTracking().Where(member => member.ProjectId == projectId && member.IsActive)
            .Join(dbContext.StudentProfiles.AsNoTracking(), member => member.StudentId, student => student.Id,
                (member, student) => new WorkspaceMemberResponse(member.StudentId, student.StudentCode ?? "Student", member.CreatedAt))
            .OrderBy(member => member.DisplayName).ToListAsync(cancellationToken);

    public async Task<(int TotalCount, IReadOnlyCollection<WorkspaceActivityResponse> Items)> ListActivityAsync(Guid projectId, string? eventType, WorkspaceActivityQuery query, CancellationToken cancellationToken)
    {
        var activityQuery = dbContext.ProjectActivities.AsNoTracking().Where(activity => activity.ProjectId == projectId);
        if (eventType is not null)
        {
            activityQuery = activityQuery.Where(activity => activity.EventType == eventType);
        }

        var total = await activityQuery.CountAsync(cancellationToken);
        var items = await activityQuery.OrderByDescending(activity => activity.CreatedAt).ThenByDescending(activity => activity.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(activity => new WorkspaceActivityResponse(activity.Id, activity.EventType, activity.CreatedAt)).ToListAsync(cancellationToken);
        return (total, items);
    }

    public async Task<IReadOnlyCollection<WorkspaceProjectSummaryRow>> ListMyProjectsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        // Correlated counts preserve a single server-side projection rather than an N+1 overview loop.
        return await dbContext.Projects.AsNoTracking()
            .Where(project => dbContext.ProjectMembers.Any(member => member.ProjectId == project.Id && member.IsActive && member.Student.UserId == userId))
            .OrderByDescending(project => project.UpdatedAt)
            .ThenByDescending(project => project.Id)
            .Select(project => new WorkspaceProjectSummaryRow(
                project.Id,
                project.Title,
                project.Slug,
                project.Status,
                dbContext.ProjectMembers.Count(member => member.ProjectId == project.Id && member.IsActive),
                dbContext.ProjectTasks.Count(task => task.ProjectId == project.Id && !task.IsDeleted && task.Status != ProjectTaskStatus.DONE),
                dbContext.ProjectTasks.Count(task => task.ProjectId == project.Id && !task.IsDeleted && task.Status != ProjectTaskStatus.DONE && task.DueAt < now)))
            .ToListAsync(cancellationToken);
    }

    public Task<WorkspaceSettings?> FindSettingsAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.WorkspaceSettings.AsNoTracking().SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);

    public Task<WorkspaceSettings?> FindSettingsForUpdateAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.WorkspaceSettings.SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);

    public void AddSettings(WorkspaceSettings settings) => dbContext.WorkspaceSettings.Add(settings);
}
