using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Applications;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the workspace data operations.</summary>
public sealed class WorkspaceRepository(AppDbContext dbContext) : IWorkspaceRepository
{
    public async Task<WorkspaceProjectRow?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var row = await dbContext.Projects.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Slug,
                item.Status,
                CompanyName = item.Company.Name,
                CompanyLogoUrl = item.Company.LogoUrl,
                item.IllustrationUrlsJson
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new WorkspaceProjectRow(
                row.Id,
                row.Title,
                row.Slug,
                row.Status,
                row.CompanyName,
                row.CompanyLogoUrl,
                Project.ParseIllustrationUrls(row.IllustrationUrlsJson));
    }

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
                             Role = member.Role,
                             AvatarUrl = student.AvatarUrl
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
            m.StudentId == leaderStudentId ? "LEADER" : (m.Role == ProjectMemberRole.MEMBER ? "MEMBER" : m.Role.ToString()),
            m.StudentId == leaderStudentId,
            m.AvatarUrl
        )).ToList();
    }

    public async Task<(string? Name, string? Email, string? Department, string? Title, string? AvatarUrl, string? Status)?> FindProjectSupervisorAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var supervisors = await LoadSupervisorsByProjectAsync([projectId], cancellationToken);
        return supervisors.TryGetValue(projectId, out var sup)
            ? (sup.Name, sup.Email, sup.Department, sup.Title, sup.AvatarUrl, sup.Status)
            : null;
    }

    private sealed record SupervisorSummary(
        string Name,
        string? Email,
        string? Department,
        string? Title,
        string? AvatarUrl,
        string Status);

    private async Task<Dictionary<Guid, SupervisorSummary>> LoadSupervisorsByProjectAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0) return new Dictionary<Guid, SupervisorSummary>();

        var rows = await (from assignment in dbContext.LecturerAssignments.AsNoTracking()
                          where projectIds.Contains(assignment.ProjectId)
                                && (assignment.Status == LecturerAssignmentStatus.ACTIVE
                                    || assignment.Status == LecturerAssignmentStatus.INVITED)
                          join lecturer in dbContext.LecturerProfiles.AsNoTracking() on assignment.LecturerId equals lecturer.Id
                          join user in dbContext.Users.AsNoTracking() on lecturer.UserId equals user.Id into userGroup
                          from u in userGroup.DefaultIfEmpty()
                          select new
                          {
                              assignment.ProjectId,
                              IsActive = assignment.Status == LecturerAssignmentStatus.ACTIVE,
                              IsPrimary = assignment.Role == LecturerAssignmentRole.PRIMARY,
                              assignment.AssignedAt,
                              Status = assignment.Status.ToString(),
                              Name = u != null && !string.IsNullOrWhiteSpace(u.DisplayName) ? u.DisplayName : lecturer.LecturerCode,
                              Email = u != null ? u.Email : null,
                              Department = lecturer.Department,
                              Title = lecturer.AcademicTitle,
                              AvatarUrl = lecturer.AvatarUrl
                          })
                          .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ProjectId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var best = g.OrderByDescending(x => x.IsActive)
                                .ThenByDescending(x => x.IsPrimary)
                                .ThenByDescending(x => x.AssignedAt)
                                .First();
                    return new SupervisorSummary(best.Name, best.Email, best.Department, best.Title, best.AvatarUrl, best.Status);
                });
    }

    public async Task<(Guid TeamId, string TeamName, string? TeamAvatarUrl)?> FindProjectTeamAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var team = await (from app in dbContext.Applications.AsNoTracking()
                          where app.ProjectId == projectId &&
                                app.Status == ApplicationStatus.ACCEPTED &&
                                app.TeamId.HasValue
                          join t in dbContext.Teams.AsNoTracking() on app.TeamId!.Value equals t.Id
                          select new { t.Id, t.Name, t.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);

        if (team is null)
        {
            team = await dbContext.Teams.AsNoTracking()
                .Where(t => t.TargetProjectId == projectId && t.IsLocked)
                .Select(t => new { t.Id, t.Name, t.AvatarUrl })
                .FirstOrDefaultAsync(cancellationToken);
        }

        return team is null ? null : (team.Id, team.Name, team.AvatarUrl);
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
        var studentId = await dbContext.StudentProfiles.AsNoTracking()
            .Where(student => student.UserId == userId)
            .Select(student => (Guid?)student.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (studentId is null) return [];

        var visibleProjectIds = await StudentProjectVisibility.ListVisibleProjectIdsAsync(dbContext, studentId.Value, cancellationToken);
        if (visibleProjectIds.Count == 0) return [];

        var now = DateTimeOffset.UtcNow;
        var projects = await dbContext.Projects.AsNoTracking()
            .Where(project => visibleProjectIds.Contains(project.Id))
            .OrderByDescending(project => project.UpdatedAt)
            .ThenByDescending(project => project.Id)
            .Select(project => new
            {
                project.Id,
                project.Title,
                project.Slug,
                project.Status,
                CompanyName = project.Company.Name,
                CompanyLogoUrl = project.Company.LogoUrl,
                project.IllustrationUrlsJson,
                MemberCount = dbContext.ProjectMembers.Count(member => member.ProjectId == project.Id && member.IsActive),
                OpenTaskCount = dbContext.ProjectTasks.Count(task => task.ProjectId == project.Id && !task.IsDeleted && task.Status != ProjectTaskStatus.DONE),
                OverdueTaskCount = dbContext.ProjectTasks.Count(task => task.ProjectId == project.Id && !task.IsDeleted && task.Status != ProjectTaskStatus.DONE && task.DueAt < now)
            })
            .ToListAsync(cancellationToken);

        if (projects.Count == 0) return [];

        var projectIds = projects.Select(p => p.Id).ToList();
        var supervisors = await LoadSupervisorsByProjectAsync(projectIds, cancellationToken);

        var acceptedAppTeams = await (from app in dbContext.Applications.AsNoTracking()
                                      where projectIds.Contains(app.ProjectId) &&
                                            app.Status == ApplicationStatus.ACCEPTED &&
                                            app.TeamId.HasValue
                                      join t in dbContext.Teams.AsNoTracking() on app.TeamId!.Value equals t.Id
                                      select new { app.ProjectId, TeamId = t.Id, TeamName = t.Name, TeamAvatarUrl = t.AvatarUrl })
                                      .ToListAsync(cancellationToken);
        var teamsByProject = new Dictionary<Guid, (Guid TeamId, string TeamName, string? TeamAvatarUrl)>();
        foreach (var row in acceptedAppTeams)
        {
            teamsByProject.TryAdd(row.ProjectId, (row.TeamId, row.TeamName, row.TeamAvatarUrl));
        }

        var lockedTargetTeams = await dbContext.Teams.AsNoTracking()
            .Where(t => t.TargetProjectId.HasValue && projectIds.Contains(t.TargetProjectId.Value) && t.IsLocked)
            .Select(t => new { ProjectId = t.TargetProjectId!.Value, t.Id, t.Name, t.AvatarUrl })
            .ToListAsync(cancellationToken);
        foreach (var lt in lockedTargetTeams)
        {
            teamsByProject.TryAdd(lt.ProjectId, (lt.Id, lt.Name, lt.AvatarUrl));
        }

        var preConfirmationProjectIds = projects.Where(p => p.MemberCount == 0).Select(p => p.Id).ToList();
        var pendingCommitmentCounts = preConfirmationProjectIds.Count > 0
            ? await dbContext.ProjectCommitments.AsNoTracking()
                .Where(c => preConfirmationProjectIds.Contains(c.ProjectId)
                    && (c.Status == CommitmentStatus.PENDING || c.Status == CommitmentStatus.CONFIRMED))
                .GroupBy(c => c.ProjectId)
                .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ProjectId, x => x.Count, cancellationToken)
            : new Dictionary<Guid, int>();

        var fallbackTeamIds = preConfirmationProjectIds
            .Select(pid => teamsByProject.TryGetValue(pid, out var info) ? (Guid?)info.TeamId : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var teamMemberCounts = fallbackTeamIds.Count > 0
            ? await dbContext.TeamMembers.AsNoTracking()
                .Where(tm => fallbackTeamIds.Contains(tm.TeamId))
                .GroupBy(tm => tm.TeamId)
                .Select(g => new { TeamId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TeamId, x => x.Count, cancellationToken)
            : new Dictionary<Guid, int>();

        return projects.Select(p =>
        {
            supervisors.TryGetValue(p.Id, out var sup);
            var hasTeam = teamsByProject.TryGetValue(p.Id, out var teamInfo);
            var effectiveMemberCount = p.MemberCount;
            if (effectiveMemberCount == 0)
            {
                if (pendingCommitmentCounts.TryGetValue(p.Id, out var cCount) && cCount > 0)
                {
                    effectiveMemberCount = cCount;
                }
                else if (hasTeam && teamMemberCounts.TryGetValue(teamInfo.TeamId, out var tmCount) && tmCount > 0)
                {
                    effectiveMemberCount = tmCount;
                }
            }
            return new WorkspaceProjectSummaryRow(
                p.Id,
                p.Title,
                p.Slug,
                p.Status,
                effectiveMemberCount,
                p.OpenTaskCount,
                p.OverdueTaskCount,
                sup?.Name,
                sup?.Email,
                sup?.Department,
                sup?.Title,
                sup?.AvatarUrl,
                hasTeam ? teamInfo.TeamId : null,
                hasTeam ? teamInfo.TeamName : null,
                hasTeam ? teamInfo.TeamAvatarUrl : null,
                p.CompanyName,
                p.CompanyLogoUrl,
                Project.ParseIllustrationUrls(p.IllustrationUrlsJson),
                sup?.Status
            );
        }).ToList();
    }

    public Task<WorkspaceSettings?> FindSettingsAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.WorkspaceSettings.AsNoTracking().SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);

    public Task<WorkspaceSettings?> FindSettingsForUpdateAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.WorkspaceSettings.SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);

    public void AddSettings(WorkspaceSettings settings) => dbContext.WorkspaceSettings.Add(settings);
}
