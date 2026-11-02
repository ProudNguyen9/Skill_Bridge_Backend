using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Lecturers;
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

    public async Task<IReadOnlyCollection<WorkspaceMemberResponse>> ListTeamAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var raw = await (from member in dbContext.ProjectMembers.AsNoTracking()
                         where member.ProjectId == projectId && member.IsActive
                         join student in dbContext.StudentProfiles.AsNoTracking() on member.StudentId equals student.Id
                         join user in dbContext.Users.AsNoTracking() on student.UserId equals user.Id into userGroup
                         from u in userGroup.DefaultIfEmpty()
                         select new
                         {
                             member.StudentId,
                             DisplayName = (u != null && !string.IsNullOrWhiteSpace(u.DisplayName)) ? u.DisplayName : (student.StudentCode ?? "Student"),
                             JoinedAt = member.CreatedAt,
                             StudentCode = student.StudentCode,
                             Email = u != null ? u.Email : null,
                             Role = member.Role
                         })
                         .OrderBy(m => m.JoinedAt)
                         .ToListAsync(cancellationToken);

        if (raw.Count == 0) return [];

        var explicitLeader = raw.FirstOrDefault(m => m.Role == ProjectMemberRole.LEADER);
        var leaderStudentId = explicitLeader?.StudentId ?? raw.First().StudentId;

        return raw.Select(m => new WorkspaceMemberResponse(
            m.StudentId,
            m.DisplayName,
            m.JoinedAt,
            m.StudentCode,
            m.Email,
            m.StudentId == leaderStudentId ? "LEADER" : m.Role.ToString(),
            m.StudentId == leaderStudentId
        )).ToList();
    }

    public async Task<(string? Name, string? Email, string? Department, string? Title)?> FindProjectSupervisorAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var supervisor = await (from assignment in dbContext.LecturerAssignments.AsNoTracking()
                                where assignment.ProjectId == projectId && assignment.Status == LecturerAssignmentStatus.ACTIVE
                                join lecturer in dbContext.LecturerProfiles.AsNoTracking() on assignment.LecturerId equals lecturer.Id
                                join user in dbContext.Users.AsNoTracking() on lecturer.UserId equals user.Id into userGroup
                                from u in userGroup.DefaultIfEmpty()
                                select new
                                {
                                    Name = u != null && !string.IsNullOrWhiteSpace(u.DisplayName) ? u.DisplayName : lecturer.LecturerCode,
                                    Email = u != null ? u.Email : null,
                                    Department = lecturer.Department,
                                    Title = lecturer.AcademicTitle
                                })
                                .FirstOrDefaultAsync(cancellationToken);

        return supervisor is null ? null : (supervisor.Name, supervisor.Email, supervisor.Department, supervisor.Title);
    }

    public async Task<bool> AssignProjectLeaderAsync(Guid projectId, Guid newLeaderStudentId, CancellationToken cancellationToken)
    {
        var members = await dbContext.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.IsActive)
            .ToListAsync(cancellationToken);

        var targetMember = members.FirstOrDefault(m => m.StudentId == newLeaderStudentId);
        if (targetMember is null) return false;

        foreach (var m in members)
        {
            if (m.StudentId == newLeaderStudentId)
            {
                m.AssignRole(ProjectMemberRole.LEADER);
            }
            else if (m.Role == ProjectMemberRole.LEADER)
            {
                m.AssignRole(ProjectMemberRole.STUDENT);
            }
        }

        return true;
    }

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
        var projects = await dbContext.Projects.AsNoTracking()
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
                OverdueTaskCount = dbContext.ProjectTasks.Count(task => task.ProjectId == project.Id && !task.IsDeleted && task.Status != ProjectTaskStatus.DONE && task.DueAt < now),
                Supervisor = (from assignment in dbContext.LecturerAssignments
                              where assignment.ProjectId == project.Id && assignment.Status == LecturerAssignmentStatus.ACTIVE
                              join lecturer in dbContext.LecturerProfiles on assignment.LecturerId equals lecturer.Id
                              join user in dbContext.Users on lecturer.UserId equals user.Id into userGroup
                              from u in userGroup.DefaultIfEmpty()
                              select new
                              {
                                  Name = u != null && !string.IsNullOrWhiteSpace(u.DisplayName) ? u.DisplayName : lecturer.LecturerCode,
                                  Email = u != null ? u.Email : null,
                                  Department = lecturer.Department,
                                  Title = lecturer.AcademicTitle
                              }).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return projects.Select(p => new WorkspaceProjectSummaryRow(
            p.Id,
            p.Title,
            p.Slug,
            p.Status,
            p.MemberCount,
            p.OpenTaskCount,
            p.OverdueTaskCount,
            p.Supervisor?.Name,
            p.Supervisor?.Email,
            p.Supervisor?.Department,
            p.Supervisor?.Title
        )).ToList();
    }

    public Task<WorkspaceSettings?> FindSettingsAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.WorkspaceSettings.AsNoTracking().SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);

    public Task<WorkspaceSettings?> FindSettingsForUpdateAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.WorkspaceSettings.SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);

    public void AddSettings(WorkspaceSettings settings) => dbContext.WorkspaceSettings.Add(settings);
}
