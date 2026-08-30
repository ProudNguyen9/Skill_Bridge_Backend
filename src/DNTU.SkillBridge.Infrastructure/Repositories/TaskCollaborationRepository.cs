using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the task collaboration data operations.</summary>
public sealed class TaskCollaborationRepository(AppDbContext dbContext) : ITaskCollaborationRepository
{
    public void AddComment(TaskComment comment) => dbContext.TaskComments.Add(comment);

    public async Task<IReadOnlyCollection<TaskCommentResponse>> ListCommentsAsync(Guid taskId, CancellationToken cancellationToken) =>
        await dbContext.TaskComments.AsNoTracking()
            .Where(comment => comment.ProjectTaskId == taskId)
            .OrderBy(comment => comment.CreatedAt).ThenBy(comment => comment.Id)
            .Select(comment => new TaskCommentResponse(comment.Id, comment.ProjectTaskId, comment.AuthorUserId, comment.Content, comment.CreatedAt))
            .ToListAsync(cancellationToken);

    public Task<TaskComment?> FindCommentAsync(Guid commentId, Guid taskId, CancellationToken cancellationToken) =>
        dbContext.TaskComments.SingleOrDefaultAsync(item => item.Id == commentId && item.ProjectTaskId == taskId, cancellationToken);

    public void RemoveComment(TaskComment comment) => dbContext.TaskComments.Remove(comment);

    public Task<TaskChecklistItem?> FindChecklistItemAsync(Guid itemId, CancellationToken cancellationToken) =>
        dbContext.TaskChecklistItems.SingleOrDefaultAsync(entry => entry.Id == itemId, cancellationToken);

    public Task<int?> GetMaxChecklistSortOrderAsync(Guid taskId, CancellationToken cancellationToken) =>
        dbContext.TaskChecklistItems.Where(item => item.ProjectTaskId == taskId).Select(item => (int?)item.SortOrder).MaxAsync(cancellationToken);

    public void AddChecklistItem(TaskChecklistItem item) => dbContext.TaskChecklistItems.Add(item);

    public void RemoveChecklistItem(TaskChecklistItem item) => dbContext.TaskChecklistItems.Remove(item);

    public void MarkTaskForChecklistUpdate(ProjectTask task, Guid expectedVersion)
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

    public Task<ProjectTask?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        dbContext.ProjectTasks.SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken);

    public Task<bool> HasCompanyManagerAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member =>
            member.UserId == userId && (member.Role == CompanyMemberRole.OWNER || member.Role == CompanyMemberRole.MANAGER)), cancellationToken);

    public async Task<(int TotalCount, IReadOnlyCollection<ProjectTaskResponse> Items)> ListStudentTasksAsync(Guid userId, TaskListQuery query, CancellationToken cancellationToken)
    {
        var tasks = dbContext.ProjectTasks.AsNoTracking().Where(task => !task.IsDeleted && task.AssigneeStudentId.HasValue &&
            dbContext.ProjectMembers.Any(member => member.ProjectId == task.ProjectId && member.StudentId == task.AssigneeStudentId && member.IsActive && member.Student.UserId == userId));
        return await ProjectTasksPageAsync(tasks, query, cancellationToken);
    }

    public async Task<(int TotalCount, IReadOnlyCollection<ProjectTaskResponse> Items)> ListLecturerTasksAsync(Guid userId, TaskListQuery query, CancellationToken cancellationToken)
    {
        var tasks = dbContext.ProjectTasks.AsNoTracking().Where(task => !task.IsDeleted &&
            dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles, assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer })
                .Any(row => row.assignment.ProjectId == task.ProjectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId));
        return await ProjectTasksPageAsync(tasks, query, cancellationToken);
    }

    private async Task<(int TotalCount, IReadOnlyCollection<ProjectTaskResponse> Items)> ProjectTasksPageAsync(IQueryable<ProjectTask> source, TaskListQuery query, CancellationToken cancellationToken)
    {
        if (query.Status is ProjectTaskStatus status) source = source.Where(task => task.Status == status);
        if (query.Priority is ProjectTaskPriority priority) source = source.Where(task => task.Priority == priority);
        if (query.AssignedOnly) source = source.Where(task => task.AssigneeStudentId.HasValue);
        if (query.Overdue) source = source.Where(task => task.DueAt < DateTimeOffset.UtcNow && task.Status != ProjectTaskStatus.DONE);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderBy(task => task.DueAt == null).ThenBy(task => task.DueAt).ThenByDescending(task => task.CreatedAt).ThenBy(task => task.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(task => new ProjectTaskResponse(task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority, task.AssigneeStudentId, task.DueAt, task.SortOrder, task.Version, task.CreatedAt)).ToListAsync(cancellationToken);
        return (total, items);
    }
}
