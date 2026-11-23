using DNTU.SkillBridge.Application.Lecturers;
using DNTU.SkillBridge.Application.Teams;
using DNTU.SkillBridge.Domain.Applications;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Teams;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the lecturer profile and supervision assignment data operations.</summary>
public sealed class LecturerRepository(AppDbContext dbContext) : ILecturerRepository
{
    public Task<LecturerProfile?> FindProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles
            .AsTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

    public Task<LecturerProfile?> FindProfileForUpdateAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == lecturerId, cancellationToken);

    public Task<string?> GetLecturerDisplayNameAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> GetProjectTitleAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.Title)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task UpdateUserDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return;
        var user = await dbContext.Users.AsTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        user?.SetDisplayName(displayName);
    }

    public void AddProfile(LecturerProfile profile) => dbContext.LecturerProfiles.Add(profile);

    public void AddAssignment(LecturerAssignment assignment) => dbContext.LecturerAssignments.Add(assignment);

    public Task<LecturerAssignment?> FindAssignmentForUpdateAsync(Guid assignmentId, Guid lecturerId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == assignmentId && item.LecturerId == lecturerId, cancellationToken);

    public Task<LecturerAssignment?> FindAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);

    public void RemoveAssignment(LecturerAssignment assignment) => dbContext.LecturerAssignments.Remove(assignment);

    public Task<bool> HasAssignmentForProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments
            .AnyAsync(item => item.ProjectId == projectId &&
                              (item.Status == LecturerAssignmentStatus.INVITED || item.Status == LecturerAssignmentStatus.ACTIVE),
                cancellationToken);

    public async Task<IReadOnlyCollection<LecturerAssignment>> ListAssignmentsAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        await dbContext.LecturerAssignments
            .AsNoTracking()
            .Where(item => item.LecturerId == lecturerId)
            .OrderBy(item => item.AssignedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    public Task<IReadOnlyCollection<LecturerAssignmentResponse>> ListEnrichedAssignmentsAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        ProjectAssignmentsQuery(
            dbContext.LecturerAssignments.AsNoTracking()
                .Where(item => item.LecturerId == lecturerId)
                .OrderByDescending(item => item.AssignedAt)
                .ThenBy(item => item.Id),
            cancellationToken);

    public Task<IReadOnlyCollection<LecturerAssignmentResponse>> ListProjectAssignmentsAsync(Guid projectId, CancellationToken cancellationToken) =>
        ProjectAssignmentsQuery(
            dbContext.LecturerAssignments.AsNoTracking()
                .Where(item => item.ProjectId == projectId)
                .OrderByDescending(item => item.AssignedAt),
            cancellationToken);

    public async Task<IReadOnlyCollection<LecturerAssignmentResponse>> ListSentProjectInvitationsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var companyProjectIds = await dbContext.Projects.AsNoTracking()
            .Where(p => p.Company.Members.Any(m => m.UserId == userId))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var studentId = await dbContext.StudentProfiles.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => (Guid?)s.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var studentProjectIds = new List<Guid>();
        if (studentId.HasValue)
        {
            var memberProjectIds = await dbContext.ProjectMembers.AsNoTracking()
                .Where(pm => pm.StudentId == studentId.Value && pm.IsActive)
                .Select(pm => pm.ProjectId)
                .ToListAsync(cancellationToken);

            var commitmentProjectIds = await dbContext.ProjectCommitments.AsNoTracking()
                .Where(pc => pc.StudentId == studentId.Value)
                .Select(pc => pc.ProjectId)
                .ToListAsync(cancellationToken);

            var teamIds = await dbContext.TeamMembers.AsNoTracking()
                .Where(tm => tm.StudentId == studentId.Value)
                .Select(tm => tm.TeamId)
                .ToListAsync(cancellationToken);

            var appProjectIds = await dbContext.Applications.AsNoTracking()
                .Where(a => a.Status != ApplicationStatus.WITHDRAWN
                            && a.Status != ApplicationStatus.REJECTED
                            && (a.StudentId == studentId.Value || (a.TeamId.HasValue && teamIds.Contains(a.TeamId.Value))))
                .Select(a => a.ProjectId)
                .ToListAsync(cancellationToken);

            var targetTeamProjectIds = teamIds.Count == 0
                ? []
                : await dbContext.Teams.AsNoTracking()
                    .Where(t => teamIds.Contains(t.Id) && t.TargetProjectId.HasValue)
                    .Select(t => t.TargetProjectId!.Value)
                    .ToListAsync(cancellationToken);

            studentProjectIds.AddRange(memberProjectIds);
            studentProjectIds.AddRange(commitmentProjectIds);
            studentProjectIds.AddRange(appProjectIds);
            studentProjectIds.AddRange(targetTeamProjectIds);
        }

        var accessibleProjectIds = companyProjectIds
            .Concat(studentProjectIds)
            .Distinct()
            .ToList();

        return await ProjectAssignmentsQuery(
            dbContext.LecturerAssignments.AsNoTracking()
                .Where(item => item.InvitedByUserId == userId || accessibleProjectIds.Contains(item.ProjectId))
                .OrderByDescending(item => item.AssignedAt)
                .ThenByDescending(item => item.Id),
            cancellationToken);
    }

    public async Task<LecturerAssignmentResponse?> ProjectAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var items = await ProjectAssignmentsQuery(
            dbContext.LecturerAssignments.AsNoTracking().Where(item => item.Id == assignmentId),
            cancellationToken);
        return items.SingleOrDefault();
    }

    public async Task<IReadOnlyCollection<LecturerDirectoryItemResponse>> SearchDirectoryAsync(string? query, int limit, CancellationToken cancellationToken)
    {
        var q = (
            from lp in dbContext.LecturerProfiles.AsNoTracking()
            join u in dbContext.Users.AsNoTracking() on lp.UserId equals u.Id
            where lp.IsActive && lp.ApprovalStatus == LecturerApprovalStatus.APPROVED && u.IsActive
            select new { lp, u });

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.Trim().ToUpperInvariant();
            q = q.Where(x =>
                x.u.DisplayName.ToUpper().Contains(normalized) ||
                x.u.NormalizedEmail.Contains(normalized) ||
                (x.lp.LecturerCode != null && x.lp.LecturerCode.ToUpper().Contains(normalized)) ||
                (x.lp.Department != null && x.lp.Department.ToUpper().Contains(normalized)));
        }

        return await q
            .OrderBy(x => x.u.DisplayName)
            .Take(limit)
            .Select(x => new LecturerDirectoryItemResponse(
                x.lp.Id,
                x.lp.UserId,
                x.u.DisplayName,
                x.u.Email,
                x.lp.LecturerCode,
                x.lp.Department,
                x.lp.AcademicTitle,
                x.lp.Bio,
                x.lp.OfficeLocation,
                x.lp.IsActive,
                x.lp.AvatarUrl))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ProjectExistsAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AsNoTracking().AnyAsync(p => p.Id == projectId && p.IsActive, cancellationToken);

    public async Task<bool> CanUserInviteLecturerForProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var isCompanyMember = await dbContext.Projects.AsNoTracking()
            .AnyAsync(p => p.Id == projectId && p.Company.Members.Any(m => m.UserId == userId), cancellationToken);
        if (isCompanyMember) return true;

        var studentId = await dbContext.StudentProfiles.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => (Guid?)s.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!studentId.HasValue) return false;

        var isProjectMember = await dbContext.ProjectMembers.AsNoTracking()
            .AnyAsync(pm => pm.ProjectId == projectId && pm.StudentId == studentId.Value && pm.IsActive, cancellationToken);
        if (isProjectMember) return true;

        var hasCommitment = await dbContext.ProjectCommitments.AsNoTracking()
            .AnyAsync(pc => pc.ProjectId == projectId && pc.StudentId == studentId.Value, cancellationToken);
        if (hasCommitment) return true;

        var teamIds = await dbContext.TeamMembers.AsNoTracking()
            .Where(tm => tm.StudentId == studentId.Value)
            .Select(tm => tm.TeamId)
            .ToListAsync(cancellationToken);

        var hasTeamApplication = await dbContext.Applications.AsNoTracking()
            .AnyAsync(a => a.ProjectId == projectId &&
                           a.TeamId.HasValue &&
                           (a.StudentId == studentId.Value || teamIds.Contains(a.TeamId.Value)) &&
                           a.Status != ApplicationStatus.WITHDRAWN &&
                           a.Status != ApplicationStatus.REJECTED,
                cancellationToken);
        if (hasTeamApplication) return true;

        return teamIds.Count > 0 && await dbContext.Teams.AsNoTracking()
            .AnyAsync(t => teamIds.Contains(t.Id) && t.TargetProjectId == projectId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> ListProjectParticipantUserIdsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var studentUserIds = await dbContext.ProjectMembers.AsNoTracking()
            .Where(pm => pm.ProjectId == projectId && pm.IsActive)
            .Select(pm => pm.Student.UserId)
            .ToListAsync(cancellationToken);

        var companyUserIds = await dbContext.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .SelectMany(p => p.Company.Members.Select(m => m.UserId))
            .ToListAsync(cancellationToken);

        return studentUserIds.Concat(companyUserIds).Distinct().ToList();
    }

    private static readonly ApplicationStatus[] ActiveTeamAppStatuses =
    [
        ApplicationStatus.ACCEPTED,
        ApplicationStatus.WAITING_MEMBER_CONSENT,
        ApplicationStatus.PENDING,
        ApplicationStatus.SHORTLISTED,
        ApplicationStatus.ADJUSTMENT_REQUESTED,
        ApplicationStatus.WAITING_LEADER_APPROVAL
    ];

    public async Task<IReadOnlyCollection<LecturerTeamItemResponse>> ListTeamsForLecturerAsync(string? query, CancellationToken cancellationToken)
    {
        var teamsQuery = dbContext.Teams.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.Trim().ToUpperInvariant();
            teamsQuery = teamsQuery.Where(t =>
                t.Name.ToUpper().Contains(normalized) ||
                dbContext.TeamMembers.Any(tm => tm.TeamId == t.Id &&
                    dbContext.StudentProfiles.Any(sp => sp.Id == tm.StudentId &&
                        ((sp.StudentCode != null && sp.StudentCode.ToUpper().Contains(normalized)) ||
                         dbContext.Users.Any(u => u.Id == sp.UserId && u.DisplayName.ToUpper().Contains(normalized))))));
        }

        var teams = await teamsQuery
            .OrderByDescending(t => t.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        if (teams.Count == 0) return [];

        var teamIds = teams.Select(t => t.Id).ToList();
        var members = await (
            from m in dbContext.TeamMembers.AsNoTracking()
            join sp in dbContext.StudentProfiles.AsNoTracking() on m.StudentId equals sp.Id
            join u in dbContext.Users.AsNoTracking() on sp.UserId equals u.Id
            where teamIds.Contains(m.TeamId)
            orderby m.Role
            select new { m.TeamId, m.Role, u.DisplayName, sp.StudentCode })
            .ToListAsync(cancellationToken);

        var targetProjectIds = teams
            .Where(t => t.IsLocked && t.TargetProjectId.HasValue)
            .Select(t => t.TargetProjectId!.Value)
            .Distinct()
            .ToList();

        var lockedProjects = targetProjectIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Projects.AsNoTracking()
                .Where(p => targetProjectIds.Contains(p.Id)
                            && p.Status != ProjectStatus.COMPLETED
                            && p.Status != ProjectStatus.CANCELLED)
                .ToDictionaryAsync(p => p.Id, p => p.Title, cancellationToken);

        var activeApplications = await (
            from a in dbContext.Applications.AsNoTracking()
            join p in dbContext.Projects.AsNoTracking() on a.ProjectId equals p.Id
            where a.TeamId.HasValue
                  && teamIds.Contains(a.TeamId.Value)
                  && ActiveTeamAppStatuses.Contains(a.Status)
                  && p.Status != ProjectStatus.COMPLETED
                  && p.Status != ProjectStatus.CANCELLED
            select new { TeamId = a.TeamId!.Value, a.Status, ProjectTitle = p.Title })
            .ToListAsync(cancellationToken);

        var activeAppByTeam = activeApplications
            .GroupBy(x => x.TeamId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Status == ApplicationStatus.ACCEPTED).First());

        return teams
            .Select(t =>
            {
                var tm = members.Where(m => m.TeamId == t.Id).ToList();
                var leader = tm.FirstOrDefault(m => m.Role == TeamMemberRole.LEADER) ?? tm.FirstOrDefault();

                var isBusy = false;
                string? busyReason = null;
                string? currentProjectTitle = null;

                if (t.IsLocked && t.TargetProjectId.HasValue &&
                    lockedProjects.TryGetValue(t.TargetProjectId.Value, out var lockedTitle))
                {
                    isBusy = true;
                    busyReason = "Đang thực hiện dự án";
                    currentProjectTitle = lockedTitle;
                }
                else if (activeAppByTeam.TryGetValue(t.Id, out var appInfo))
                {
                    isBusy = true;
                    busyReason = appInfo.Status == ApplicationStatus.ACCEPTED
                        ? "Đã trúng tuyển dự án"
                        : "Đang có đơn ứng tuyển";
                    currentProjectTitle = appInfo.ProjectTitle;
                }

                return new
                {
                    t.CreatedAt,
                    Item = new LecturerTeamItemResponse(
                        t.Id,
                        t.Name,
                        t.Description,
                        t.TeamType.ToString(),
                        leader?.DisplayName ?? "Trưởng nhóm",
                        leader?.StudentCode,
                        tm.Count,
                        tm.Select(m => m.DisplayName).ToList(),
                        t.AvatarUrl,
                        isBusy,
                        busyReason,
                        currentProjectTitle)
                };
            })
            .OrderBy(x => x.Item.IsBusy)
            .ThenByDescending(x => x.Item.MemberCount)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => x.Item)
            .ToList();
    }

    public async Task<bool> IsTeamBusyWithAnotherProjectAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var hasActiveApp = await dbContext.Applications.AsNoTracking()
            .AnyAsync(a => a.TeamId == teamId &&
                           ActiveTeamAppStatuses.Contains(a.Status) &&
                           a.Project.Status != ProjectStatus.COMPLETED &&
                           a.Project.Status != ProjectStatus.CANCELLED,
                cancellationToken);
        if (hasActiveApp) return true;

        return await dbContext.Teams.AsNoTracking()
            .AnyAsync(t => t.Id == teamId &&
                           t.IsLocked &&
                           t.TargetProjectId.HasValue &&
                           dbContext.Projects.Any(p => p.Id == t.TargetProjectId.Value &&
                                                       p.Status != ProjectStatus.COMPLETED &&
                                                       p.Status != ProjectStatus.CANCELLED),
                cancellationToken);
    }

    public async Task<(Guid LeaderUserId, string TeamName)?> FindTeamLeaderInfoAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var row = await (
            from t in dbContext.Teams.AsNoTracking()
            join m in dbContext.TeamMembers.AsNoTracking() on t.Id equals m.TeamId
            join sp in dbContext.StudentProfiles.AsNoTracking() on m.StudentId equals sp.Id
            where t.Id == teamId && m.Role == TeamMemberRole.LEADER
            select new { sp.UserId, t.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : (row.UserId, row.Name);
    }

    public Task<bool> HasPendingRecommendationAsync(Guid lecturerId, Guid teamId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.TeamProjectRecommendations.AsNoTracking()
            .AnyAsync(r => r.LecturerId == lecturerId && r.TeamId == teamId && r.ProjectId == projectId && r.Status == TeamRecommendationStatus.PENDING, cancellationToken);

    public void AddRecommendation(TeamProjectRecommendation recommendation) => dbContext.TeamProjectRecommendations.Add(recommendation);

    public async Task<IReadOnlyCollection<TeamProjectRecommendationResponse>> ListLecturerRecommendationsAsync(Guid lecturerId, CancellationToken cancellationToken)
    {
        return await (
            from rec in dbContext.TeamProjectRecommendations.AsNoTracking()
            join lec in dbContext.LecturerProfiles.AsNoTracking() on rec.LecturerId equals lec.Id
            join lecUser in dbContext.Users.AsNoTracking() on lec.UserId equals lecUser.Id
            join team in dbContext.Teams.AsNoTracking() on rec.TeamId equals team.Id
            join proj in dbContext.Projects.AsNoTracking() on rec.ProjectId equals proj.Id
            join comp in dbContext.Companies.AsNoTracking() on proj.CompanyId equals comp.Id
            where rec.LecturerId == lecturerId
            orderby rec.CreatedAt descending
            select new TeamProjectRecommendationResponse(
                rec.Id,
                rec.LecturerId,
                lecUser.DisplayName,
                lec.Department,
                lec.AcademicTitle,
                rec.TeamId,
                team.Name,
                rec.ProjectId,
                proj.Title,
                proj.Slug,
                comp.Name,
                rec.Reason,
                rec.Status,
                rec.CreatedAt,
                rec.RespondedAt,
                rec.ResponseNote,
                team.AvatarUrl))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<LecturerAssignmentResponse>> ProjectAssignmentsQuery(
        IQueryable<LecturerAssignment> query, CancellationToken cancellationToken)
    {
        var rows = await query.ToListAsync(cancellationToken);
        if (rows.Count == 0) return [];

        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var lecturerIds = rows.Select(r => r.LecturerId).Distinct().ToList();
        var teamIds = rows.Where(r => r.TeamId.HasValue).Select(r => r.TeamId!.Value).Distinct().ToList();
        var inviterIds = rows.Where(r => r.InvitedByUserId.HasValue).Select(r => r.InvitedByUserId!.Value).Distinct().ToList();

        var projects = await (
            from p in dbContext.Projects.AsNoTracking()
            join c in dbContext.Companies.AsNoTracking() on p.CompanyId equals c.Id
            where projectIds.Contains(p.Id)
            select new
            {
                p.Id,
                p.Title,
                p.Slug,
                Status = p.Status.ToString(),
                CompanyName = c.Name,
                CompanyLogoUrl = c.LogoUrl,
                p.IllustrationUrlsJson,
                MemberCount = dbContext.ProjectMembers.Count(pm => pm.ProjectId == p.Id && pm.IsActive)
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var lecturers = await (
            from lp in dbContext.LecturerProfiles.AsNoTracking()
            join u in dbContext.Users.AsNoTracking() on lp.UserId equals u.Id
            where lecturerIds.Contains(lp.Id)
            select new { lp.Id, u.DisplayName, u.Email, lp.Department, lp.AcademicTitle, lp.AvatarUrl, UserId = (Guid?)u.Id })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var teams = teamIds.Count == 0
            ? new Dictionary<Guid, (string Name, string? AvatarUrl)>()
            : (await dbContext.Teams.AsNoTracking()
                .Where(t => teamIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name, t.AvatarUrl })
                .ToListAsync(cancellationToken))
                .ToDictionary(t => t.Id, t => (t.Name, t.AvatarUrl));

        var inviters = inviterIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Users.AsNoTracking().Where(u => inviterIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        return rows.Select(a =>
        {
            projects.TryGetValue(a.ProjectId, out var proj);
            lecturers.TryGetValue(a.LecturerId, out var lec);
            (string Name, string? AvatarUrl) teamInfo = default;
            var hasTeam = a.TeamId.HasValue && teams.TryGetValue(a.TeamId.Value, out teamInfo);
            var inviterName = a.InvitedByUserId.HasValue ? inviters.GetValueOrDefault(a.InvitedByUserId.Value) : null;

            return new LecturerAssignmentResponse(
                a.Id,
                a.LecturerId,
                a.ProjectId,
                a.Role.ToString(),
                a.Status.ToString(),
                a.AssignedAt,
                a.AcceptedAt,
                a.EndedAt,
                a.Note,
                a.TeamId,
                hasTeam ? teamInfo.Name : null,
                a.InvitedByUserId,
                inviterName,
                proj?.Title,
                proj?.Slug,
                proj?.Status,
                proj?.CompanyName,
                lec?.DisplayName,
                lec?.Email,
                lec?.Department,
                lec?.AcademicTitle,
                a.ExpectedWork,
                a.ExpectedDuration,
                a.AllowanceShareAmount,
                a.IsCourseLinked,
                a.CourseName,
                a.RespondedAt,
                a.DeclineReason,
                proj?.MemberCount ?? 0,
                hasTeam ? teamInfo.AvatarUrl : null,
                lec?.AvatarUrl,
                proj?.CompanyLogoUrl,
                Project.ParseIllustrationUrls(proj?.IllustrationUrlsJson),
                lec?.UserId);
        }).ToList();
    }
}
