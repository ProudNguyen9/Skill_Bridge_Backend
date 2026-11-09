using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the project task data operations.</summary>
public sealed class ProjectTaskRepository(AppDbContext dbContext) : IProjectTaskRepository
{
    public async Task<bool> IsMemberAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        // 1. Active student member
        if (await dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken))
            return true;

        // 2. Company member of the project
        if (await dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member => member.UserId == userId), cancellationToken))
            return true;

        // 3. Assigned active lecturer
        if (await dbContext.LecturerAssignments
            .Join(dbContext.LecturerProfiles, a => a.LecturerId, l => l.Id, (a, l) => new { a, l })
            .AnyAsync(item => item.a.ProjectId == projectId && item.a.Status == LecturerAssignmentStatus.ACTIVE && item.l.UserId == userId, cancellationToken))
            return true;

        // 4. Admin or SuperAdmin
        if (await dbContext.UserRoles.AnyAsync(ur => ur.UserId == userId && (ur.Role.Name == RoleNames.Admin || ur.Role.Name == RoleNames.SuperAdmin), cancellationToken))
            return true;

        return false;
    }

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

    public async Task<IReadOnlyCollection<ProjectTaskResponse>> ListBoardTasksAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var rawTasks = await (from task in dbContext.ProjectTasks.AsNoTracking()
                              where task.ProjectId == projectId && !task.IsDeleted
                              join student in dbContext.StudentProfiles.AsNoTracking() on task.AssigneeStudentId equals student.Id into studentGroup
                              from s in studentGroup.DefaultIfEmpty()
                              join user in dbContext.Users.AsNoTracking() on s.UserId equals user.Id into userGroup
                              from u in userGroup.DefaultIfEmpty()
                              orderby task.SortOrder
                              select new
                              {
                                  task.Id,
                                  task.ProjectId,
                                  task.Title,
                                  task.Description,
                                  task.Status,
                                  task.Priority,
                                  task.AssigneeStudentId,
                                  task.DueAt,
                                  task.SortOrder,
                                  task.Version,
                                  task.CreatedAt,
                                  AssigneeName = (u != null && !string.IsNullOrWhiteSpace(u.DisplayName))
                                      ? u.DisplayName
                                      : (s != null && !string.IsNullOrWhiteSpace(s.StudentCode) ? s.StudentCode : null),
                                  AssigneeAvatarUrl = s != null ? s.AvatarUrl : null
                              }).ToListAsync(cancellationToken);

        return rawTasks.Select(t => new ProjectTaskResponse(
            t.Id,
            t.ProjectId,
            t.Title,
            t.Description,
            t.Status,
            t.Priority,
            t.AssigneeStudentId,
            t.DueAt,
            t.SortOrder,
            t.Version,
            t.CreatedAt,
            t.AssigneeName,
            t.AssigneeAvatarUrl
        )).ToList();
    }
}
