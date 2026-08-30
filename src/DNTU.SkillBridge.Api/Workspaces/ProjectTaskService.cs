using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Workspaces;

public enum ProjectTaskOutcome { Success, NotFound, Forbidden, Conflict, InvalidAssignee }

public sealed class ProjectTaskService(AppDbContext dbContext)
{
    public async Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> CreateAsync(Guid userId, Guid projectId, CreateProjectTaskRequest request, CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(userId, projectId, cancellationToken)) return (ProjectTaskOutcome.Forbidden, null);
        var nextOrder = await dbContext.ProjectTasks.Where(task => task.ProjectId == projectId && task.Status == ProjectTaskStatus.BACKLOG && !task.IsDeleted).Select(task => (int?)task.SortOrder).MaxAsync(cancellationToken) ?? -1;
        var task = new ProjectTask(projectId, request.Title, request.Description, request.Priority, request.DueAt, nextOrder + 1);
        dbContext.ProjectTasks.Add(task);
        dbContext.ProjectActivities.Add(new ProjectActivity(projectId, "TASK_CREATED", userId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return (ProjectTaskOutcome.Success, Map(task));
    }

    public async Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await dbContext.ProjectTasks.AsNoTracking().SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken);
        if (task is null) return (ProjectTaskOutcome.NotFound, null);
        return await IsMemberAsync(userId, task.ProjectId, cancellationToken) ? (ProjectTaskOutcome.Success, Map(task)) : (ProjectTaskOutcome.Forbidden, null);
    }

    public async Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> UpdateAsync(Guid userId, Guid taskId, UpdateProjectTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await dbContext.ProjectTasks.SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken);
        if (task is null) return (ProjectTaskOutcome.NotFound, null);
        if (!await IsMemberAsync(userId, task.ProjectId, cancellationToken)) return (ProjectTaskOutcome.Forbidden, null);
        try { task.Update(request.Title, request.Description, request.Priority, request.DueAt, request.Version); }
        catch (InvalidOperationException) { return (ProjectTaskOutcome.Conflict, null); }
        MarkForUpdate(task, request.Version);
        dbContext.ProjectActivities.Add(new ProjectActivity(task.ProjectId, "TASK_UPDATED", userId));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (ProjectTaskOutcome.Conflict, null);
        }
        return (ProjectTaskOutcome.Success, Map(task));
    }

    public async Task<ProjectTaskOutcome> DeleteAsync(Guid userId, Guid taskId, Guid version, CancellationToken cancellationToken)
    {
        var task = await dbContext.ProjectTasks.SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken);
        if (task is null) return ProjectTaskOutcome.NotFound;
        if (!await IsMemberAsync(userId, task.ProjectId, cancellationToken)) return ProjectTaskOutcome.Forbidden;
        try { task.Delete(version); }
        catch (InvalidOperationException) { return ProjectTaskOutcome.Conflict; }
        MarkForUpdate(task, version);
        dbContext.ProjectActivities.Add(new ProjectActivity(task.ProjectId, "TASK_DELETED", userId));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ProjectTaskOutcome.Conflict;
        }
        return ProjectTaskOutcome.Success;
    }

    public async Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> MoveAsync(Guid userId, Guid taskId, MoveProjectTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await dbContext.ProjectTasks.SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken);
        if (task is null) return (ProjectTaskOutcome.NotFound, null);
        if (!await IsMemberAsync(userId, task.ProjectId, cancellationToken)) return (ProjectTaskOutcome.Forbidden, null);
        try { task.Move(request.Status, request.SortOrder, request.Version); }
        catch (InvalidOperationException) { return (ProjectTaskOutcome.Conflict, null); }
        MarkForUpdate(task, request.Version);
        dbContext.ProjectActivities.Add(new ProjectActivity(task.ProjectId, "TASK_MOVED", userId));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (ProjectTaskOutcome.Conflict, null);
        }
        return (ProjectTaskOutcome.Success, Map(task));
    }

    public async Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> AssignAsync(Guid userId, Guid taskId, AssignProjectTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await dbContext.ProjectTasks.SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken);
        if (task is null) return (ProjectTaskOutcome.NotFound, null);
        if (!await IsMemberAsync(userId, task.ProjectId, cancellationToken)) return (ProjectTaskOutcome.Forbidden, null);
        if (request.StudentId.HasValue && !await dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == task.ProjectId && member.StudentId == request.StudentId.Value && member.IsActive, cancellationToken)) return (ProjectTaskOutcome.InvalidAssignee, null);
        try { task.Assign(request.StudentId, request.Version); }
        catch (InvalidOperationException) { return (ProjectTaskOutcome.Conflict, null); }
        MarkForUpdate(task, request.Version);
        dbContext.ProjectActivities.Add(new ProjectActivity(task.ProjectId, "TASK_ASSIGNED", userId));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (ProjectTaskOutcome.Conflict, null);
        }
        return (ProjectTaskOutcome.Success, Map(task));
    }

    public async Task<KanbanBoardResponse?> BoardAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(userId, projectId, cancellationToken)) return null;
        var tasks = await dbContext.ProjectTasks.AsNoTracking().Where(task => task.ProjectId == projectId && !task.IsDeleted).OrderBy(task => task.SortOrder).Select(task => new ProjectTaskResponse(task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority, task.AssigneeStudentId, task.DueAt, task.SortOrder, task.Version, task.CreatedAt)).ToListAsync(cancellationToken);
        var columns = Enum.GetValues<ProjectTaskStatus>().ToDictionary(status => status, status => (IReadOnlyCollection<ProjectTaskResponse>)tasks.Where(task => task.Status == status).ToList());
        return new KanbanBoardResponse(projectId, columns);
    }

    private Task<bool> IsMemberAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) => dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken);

    // Domain mutations use private setters. Explicitly mark the aggregate and restore the
    // client version as EF's original concurrency value; EF then writes the renewed version
    // only when the database row still has the version the caller read.
    private void MarkForUpdate(ProjectTask task, Guid expectedVersion)
    {
        var entry = dbContext.Entry(task);
        entry.State = EntityState.Modified;
        entry.Property(item => item.Version).OriginalValue = expectedVersion;
    }

    private static ProjectTaskResponse Map(ProjectTask task) => new(task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority, task.AssigneeStudentId, task.DueAt, task.SortOrder, task.Version, task.CreatedAt);
}
