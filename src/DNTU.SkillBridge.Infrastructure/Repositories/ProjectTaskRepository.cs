using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the project task data operations.</summary>
public sealed class ProjectTaskRepository(AppDbContext dbContext) : IProjectTaskRepository
{
    public Task<bool> IsMemberAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken);

    public Task<int?> GetMaxBacklogSortOrderAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectTasks.Where(task => task.ProjectId == projectId && task.Status == ProjectTaskStatus.BACKLOG && !task.IsDeleted).Select(task => (int?)task.SortOrder).MaxAsync(cancellationToken);

    public void AddTask(ProjectTask task) => dbContext.ProjectTasks.Add(task);

    public Task<ProjectTask?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        dbContext.ProjectTasks.AsNoTracking().SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken);

    public Task<ProjectTask?> FindTaskForUpdateAsync(Guid taskId, CancellationToken cancellationToken) =>
        dbContext.ProjectTasks.SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken);

    public Task<bool> HasActiveAssigneeAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.StudentId == studentId && member.IsActive, cancellationToken);

    // Domain mutations use private setters. Explicitly mark the aggregate and restore the
    // client version as EF's original concurrency value; EF then writes the renewed version
    // only when the database row still has the version the caller read.
    public void MarkForUpdate(ProjectTask task, Guid expectedVersion)
    {
        var entry = dbContext.Entry(task);
        entry.State = EntityState.Modified;
        entry.Property(item => item.Version).OriginalValue = expectedVersion;
    }

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyCollection<ProjectTaskResponse>> ListBoardTasksAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectTasks.AsNoTracking().Where(task => task.ProjectId == projectId && !task.IsDeleted).OrderBy(task => task.SortOrder).Select(task => new ProjectTaskResponse(task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority, task.AssigneeStudentId, task.DueAt, task.SortOrder, task.Version, task.CreatedAt)).ToListAsync(cancellationToken);
}
