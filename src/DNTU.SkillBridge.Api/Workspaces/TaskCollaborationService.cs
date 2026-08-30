using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Api.Workspaces;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Workspaces;

public sealed class TaskCollaborationService(AppDbContext dbContext, ProjectAccessService projectAccessService, IProjectActivityWriter activityWriter)
{
    public async Task<TaskCommentResponse?> AddCommentAsync(ICurrentUser currentUser, Guid taskId, CreateTaskCommentRequest request, CancellationToken cancellationToken)
    {
        var task = await AccessibleTaskAsync(currentUser, taskId, cancellationToken);
        if (task is null || currentUser.UserId is not Guid userId) return null;
        var comment = new TaskComment(task.Id, userId, request.Content);
        dbContext.TaskComments.Add(comment);
        activityWriter.Append(task.ProjectId, "TASK_COMMENT_CREATED", userId, new { taskId = task.Id, commentId = comment.Id });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TaskCommentResponse(comment.Id, comment.ProjectTaskId, comment.AuthorUserId, comment.Content, comment.CreatedAt);
    }

    public async Task<IReadOnlyCollection<TaskCommentResponse>?> ListCommentsAsync(ICurrentUser currentUser, Guid taskId, CancellationToken cancellationToken)
    {
        if (await AccessibleTaskAsync(currentUser, taskId, cancellationToken) is null) return null;
        return await dbContext.TaskComments.AsNoTracking()
            .Where(comment => comment.ProjectTaskId == taskId)
            .OrderBy(comment => comment.CreatedAt).ThenBy(comment => comment.Id)
            .Select(comment => new TaskCommentResponse(comment.Id, comment.ProjectTaskId, comment.AuthorUserId, comment.Content, comment.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteCommentAsync(ICurrentUser currentUser, Guid taskId, Guid commentId, CancellationToken cancellationToken)
    {
        var task = await AccessibleTaskAsync(currentUser, taskId, cancellationToken);
        if (task is null || currentUser.UserId is not Guid userId) return false;
        var comment = await dbContext.TaskComments.SingleOrDefaultAsync(item => item.Id == commentId && item.ProjectTaskId == taskId, cancellationToken);
        if (comment is null || (comment.AuthorUserId != userId && !await CanManageAsync(currentUser, task.ProjectId, cancellationToken))) return false;
        dbContext.TaskComments.Remove(comment);
        activityWriter.Append(task.ProjectId, "TASK_COMMENT_DELETED", userId, new { taskId = task.Id, commentId });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(TaskCollaborationOutcome Outcome, TaskChecklistItemResponse? Item)> AddChecklistItemAsync(ICurrentUser currentUser, Guid taskId, CreateChecklistItemRequest request, CancellationToken cancellationToken)
    {
        var task = await AccessibleTaskAsync(currentUser, taskId, cancellationToken);
        if (task is null) return (TaskCollaborationOutcome.NotFound, null);
        if (currentUser.UserId is not Guid userId) return (TaskCollaborationOutcome.Forbidden, null);
        try { task.UpdateChecklistVersion(request.Version); }
        catch (InvalidOperationException) { return (TaskCollaborationOutcome.Conflict, null); }

        var order = await dbContext.TaskChecklistItems.Where(item => item.ProjectTaskId == taskId).Select(item => (int?)item.SortOrder).MaxAsync(cancellationToken) ?? -1;
        var item = new TaskChecklistItem(taskId, request.Title, order + 1);
        dbContext.TaskChecklistItems.Add(item);
        MarkTaskForChecklistUpdate(task, request.Version);
        activityWriter.Append(task.ProjectId, "TASK_CHECKLIST_CREATED", userId, new { taskId = task.Id, checklistItemId = item.Id });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (TaskCollaborationOutcome.Conflict, null);
        }

        return (TaskCollaborationOutcome.Success, Map(item));
    }

    public async Task<(TaskCollaborationOutcome Outcome, TaskChecklistItemResponse? Item)> UpdateChecklistItemAsync(ICurrentUser currentUser, Guid itemId, UpdateChecklistItemRequest request, CancellationToken cancellationToken)
    {
        var item = await dbContext.TaskChecklistItems.SingleOrDefaultAsync(entry => entry.Id == itemId, cancellationToken);
        if (item is null) return (TaskCollaborationOutcome.NotFound, null);
        var task = await AccessibleTaskAsync(currentUser, item.ProjectTaskId, cancellationToken);
        if (task is null) return (TaskCollaborationOutcome.NotFound, null);
        if (currentUser.UserId is not Guid userId) return (TaskCollaborationOutcome.Forbidden, null);
        try
        {
            task.UpdateChecklistVersion(request.Version);
            item.Update(request.Title, request.IsCompleted);
        }
        catch (InvalidOperationException) { return (TaskCollaborationOutcome.Conflict, null); }

        MarkTaskForChecklistUpdate(task, request.Version);
        activityWriter.Append(task.ProjectId, "TASK_CHECKLIST_UPDATED", userId, new { taskId = task.Id, checklistItemId = item.Id });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (TaskCollaborationOutcome.Conflict, null);
        }

        return (TaskCollaborationOutcome.Success, Map(item));
    }

    public async Task<TaskCollaborationOutcome> DeleteChecklistItemAsync(ICurrentUser currentUser, Guid itemId, Guid version, CancellationToken cancellationToken)
    {
        var item = await dbContext.TaskChecklistItems.SingleOrDefaultAsync(entry => entry.Id == itemId, cancellationToken);
        if (item is null) return TaskCollaborationOutcome.NotFound;
        var task = await AccessibleTaskAsync(currentUser, item.ProjectTaskId, cancellationToken);
        if (task is null) return TaskCollaborationOutcome.NotFound;
        if (currentUser.UserId is not Guid userId) return TaskCollaborationOutcome.Forbidden;
        try { task.UpdateChecklistVersion(version); }
        catch (InvalidOperationException) { return TaskCollaborationOutcome.Conflict; }

        dbContext.TaskChecklistItems.Remove(item);
        MarkTaskForChecklistUpdate(task, version);
        activityWriter.Append(task.ProjectId, "TASK_CHECKLIST_DELETED", userId, new { taskId = task.Id, checklistItemId = item.Id });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TaskCollaborationOutcome.Conflict;
        }

        return TaskCollaborationOutcome.Success;
    }

    public async Task<PagedResponse<ProjectTaskResponse>?> GetMyStudentTasksAsync(ICurrentUser currentUser, TaskListQuery query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.Roles.Contains(RoleNames.Student) || currentUser.UserId is not Guid userId) return null;
        var tasks = dbContext.ProjectTasks.AsNoTracking().Where(task => !task.IsDeleted && task.AssigneeStudentId.HasValue &&
            dbContext.ProjectMembers.Any(member => member.ProjectId == task.ProjectId && member.StudentId == task.AssigneeStudentId && member.IsActive && member.Student.UserId == userId));
        return await ProjectTasksPageAsync(tasks, query, cancellationToken);
    }

    public async Task<PagedResponse<ProjectTaskResponse>?> GetLecturerTasksAsync(ICurrentUser currentUser, TaskListQuery query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.Roles.Contains(RoleNames.Lecturer) || currentUser.UserId is not Guid userId) return null;
        var tasks = dbContext.ProjectTasks.AsNoTracking().Where(task => !task.IsDeleted &&
            dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles, assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer })
                .Any(row => row.assignment.ProjectId == task.ProjectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId));
        return await ProjectTasksPageAsync(tasks, query, cancellationToken);
    }

    private async Task<PagedResponse<ProjectTaskResponse>> ProjectTasksPageAsync(IQueryable<ProjectTask> source, TaskListQuery query, CancellationToken cancellationToken)
    {
        if (query.Status is ProjectTaskStatus status) source = source.Where(task => task.Status == status);
        if (query.Priority is ProjectTaskPriority priority) source = source.Where(task => task.Priority == priority);
        if (query.AssignedOnly) source = source.Where(task => task.AssigneeStudentId.HasValue);
        if (query.Overdue) source = source.Where(task => task.DueAt < DateTimeOffset.UtcNow && task.Status != ProjectTaskStatus.DONE);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderBy(task => task.DueAt == null).ThenBy(task => task.DueAt).ThenByDescending(task => task.CreatedAt).ThenBy(task => task.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(task => new ProjectTaskResponse(task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority, task.AssigneeStudentId, task.DueAt, task.SortOrder, task.Version, task.CreatedAt)).ToListAsync(cancellationToken);
        return new PagedResponse<ProjectTaskResponse>(items, PageMetadata.Create(query.Page, query.PageSize, total));
    }

    private async Task<ProjectTask?> AccessibleTaskAsync(ICurrentUser currentUser, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await dbContext.ProjectTasks.SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken);
        if (task is null || !(await projectAccessService.GetAsync(currentUser, task.ProjectId, cancellationToken)).CanRead)
        {
            return null;
        }

        return task;
    }

    private async Task<bool> CanManageAsync(ICurrentUser currentUser, Guid projectId, CancellationToken cancellationToken)
    {
        if ((await projectAccessService.GetAsync(currentUser, projectId, cancellationToken)).CanManage) return true;
        return currentUser.UserId.HasValue && await dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member =>
            member.UserId == currentUser.UserId && (member.Role == CompanyMemberRole.OWNER || member.Role == CompanyMemberRole.MANAGER)), cancellationToken);
    }

    private void MarkTaskForChecklistUpdate(ProjectTask task, Guid expectedVersion)
    {
        var entry = dbContext.Entry(task);
        entry.State = EntityState.Modified;
        entry.Property(item => item.Version).OriginalValue = expectedVersion;
    }

    private static TaskChecklistItemResponse Map(TaskChecklistItem item) => new(item.Id, item.ProjectTaskId, item.Title, item.IsCompleted, item.SortOrder);
}

public enum TaskCollaborationOutcome { Success, NotFound, Forbidden, Conflict }
