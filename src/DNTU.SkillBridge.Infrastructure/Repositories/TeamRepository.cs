using DNTU.SkillBridge.Application.Teams;
using DNTU.SkillBridge.Domain.Applications;
using DNTU.SkillBridge.Domain.Common;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Domain.Teams;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the team data operations.</summary>
public sealed class TeamRepository(AppDbContext dbContext) : ITeamRepository
{
    public Task<Guid?> FindStudentProfileIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles.AsNoTracking().Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);

    public async Task<(Guid StudentId, Guid UserId, string DisplayName)?> FindStudentByIdOrIdentifierAsync(Guid? studentId, string? identifier, CancellationToken cancellationToken)
    {
        if (studentId.HasValue && studentId.Value != Guid.Empty)
        {
            var found = await (
                from profile in dbContext.StudentProfiles.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on profile.UserId equals user.Id
                where profile.Id == studentId.Value && user.IsActive
                select new { profile.Id, profile.UserId, user.DisplayName })
                .SingleOrDefaultAsync(cancellationToken);
            return found is null ? null : (found.Id, found.UserId, found.DisplayName);
        }

        if (!string.IsNullOrWhiteSpace(identifier))
        {
            var normalized = identifier.Trim().ToUpperInvariant();
            var found = await (
                from profile in dbContext.StudentProfiles.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on profile.UserId equals user.Id
                where user.IsActive &&
                      ((profile.StudentCode != null && profile.StudentCode.ToUpper() == normalized) ||
                       user.NormalizedEmail == normalized)
                select new { profile.Id, profile.UserId, user.DisplayName })
                .FirstOrDefaultAsync(cancellationToken);
            return found is null ? null : (found.Id, found.UserId, found.DisplayName);
        }

        return null;
    }

    public Task<Guid?> FindStudentUserIdAsync(Guid studentId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => (Guid?)s.UserId)
            .SingleOrDefaultAsync(cancellationToken);

    public void AddStudentProfile(StudentProfile profile) => dbContext.StudentProfiles.Add(profile);

    public Task<bool> HasTeamMembershipAsync(Guid teamId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.TeamMembers.AsNoTracking()
            .AnyAsync(member => member.TeamId == teamId && member.StudentId == studentId, cancellationToken);

    public async Task<IReadOnlyCollection<TeamResponse>> ListTeamsForStudentAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var teamIds = await dbContext.TeamMembers.AsNoTracking()
            .Where(m => m.StudentId == studentId)
            .Select(m => m.TeamId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (teamIds.Count == 0) return [];

        return await ProjectTeamsByIdsAsync(teamIds, cancellationToken);
    }

    public Task<Team?> FindTeamWithMembersAsync(Guid teamId, CancellationToken cancellationToken) =>
        dbContext.Teams.Include(item => item.Members).SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);

    public Task<TeamInvitation?> FindInvitationForStudentAsync(Guid invitationId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.TeamInvitations.Include(item => item.Team).ThenInclude(item => item.Members).SingleOrDefaultAsync(item => item.Id == invitationId && item.InvitedStudentId == studentId, cancellationToken);

    public Task<TeamInvitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken) =>
        dbContext.TeamInvitations.AsTracking().SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken);

    public Task<bool> HasStudentProfileAsync(Guid studentId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles.AsNoTracking().AnyAsync(item => item.Id == studentId, cancellationToken);

    private static readonly ApplicationStatus[] ActiveTeamAppStatuses =
    [
        ApplicationStatus.ACCEPTED,
        ApplicationStatus.WAITING_MEMBER_CONSENT,
        ApplicationStatus.PENDING,
        ApplicationStatus.SHORTLISTED,
        ApplicationStatus.ADJUSTMENT_REQUESTED,
        ApplicationStatus.WAITING_LEADER_APPROVAL
    ];

    public Task<bool> HasPendingInvitationAsync(Guid teamId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.TeamInvitations.AnyAsync(item => item.TeamId == teamId && item.InvitedStudentId == studentId && item.Status == TeamInvitationStatus.PENDING, cancellationToken);

    public async Task<bool> HasActiveProjectOrApplicationAsync(Guid teamId, Guid? excludeProjectId, CancellationToken cancellationToken)
    {
        var hasActiveApp = await dbContext.Applications.AsNoTracking()
            .AnyAsync(a => a.TeamId == teamId &&
                           ActiveTeamAppStatuses.Contains(a.Status) &&
                           a.Project.Status != ProjectStatus.COMPLETED &&
                           a.Project.Status != ProjectStatus.CANCELLED &&
                           (!excludeProjectId.HasValue || a.ProjectId != excludeProjectId.Value),
                cancellationToken);
        if (hasActiveApp) return true;

        var hasLockedUnfinishedTarget = await dbContext.Teams.AsNoTracking()
            .AnyAsync(t => t.Id == teamId &&
                           t.IsLocked &&
                           t.TargetProjectId.HasValue &&
                           (!excludeProjectId.HasValue || t.TargetProjectId.Value != excludeProjectId.Value) &&
                           dbContext.Projects.Any(p => p.Id == t.TargetProjectId.Value &&
                                                       p.Status != ProjectStatus.COMPLETED &&
                                                       p.Status != ProjectStatus.CANCELLED),
                cancellationToken);
        return hasLockedUnfinishedTarget;
    }

    public void AddTeam(Team team) => dbContext.Teams.Add(team);

    public void AddTeamInvitation(TeamInvitation invitation) => dbContext.TeamInvitations.Add(invitation);

    public void AddTeamMember(TeamMember member) => dbContext.TeamMembers.Add(member);

    public async Task<List<TeamInvitation>> FindPendingInvitationsAsync(Guid studentId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.TeamInvitations.Where(item => item.InvitedStudentId == studentId && item.Status == TeamInvitationStatus.PENDING && item.ExpiresAt <= now).ToListAsync(cancellationToken);

    public void RemoveTeam(Team team) => dbContext.Teams.Remove(team);

    public void RemoveTeamMember(TeamMember member) => dbContext.TeamMembers.Remove(member);

    public async Task<TeamResponse?> ProjectTeamAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var items = await ProjectTeamsByIdsAsync([teamId], cancellationToken);
        return items.SingleOrDefault();
    }

    public async Task<TeamInvitationResponse?> ProjectInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var rows = await ProjectInvitationsQuery(dbContext.TeamInvitations.AsNoTracking().Where(i => i.Id == invitationId), cancellationToken);
        return rows.SingleOrDefault();
    }

    public Task<IReadOnlyCollection<TeamInvitationResponse>> ListInvitationsAsync(Guid studentId, CancellationToken cancellationToken) =>
        ProjectInvitationsQuery(
            dbContext.TeamInvitations.AsNoTracking()
                .Where(item => item.InvitedStudentId == studentId)
                .OrderByDescending(item => item.CreatedAt),
            cancellationToken);

    public Task<IReadOnlyCollection<TeamInvitationResponse>> ListTeamInvitationsAsync(Guid teamId, CancellationToken cancellationToken) =>
        ProjectInvitationsQuery(
            dbContext.TeamInvitations.AsNoTracking()
                .Where(item => item.TeamId == teamId)
                .OrderByDescending(item => item.CreatedAt),
            cancellationToken);

    public async Task<IReadOnlyCollection<TeamProjectRecommendationResponse>> ListRecommendationsForTeamAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var recs = await ProjectRecommendationsAsync(dbContext.TeamProjectRecommendations.AsNoTracking().Where(r => r.TeamId == teamId), cancellationToken);
        var activeProjectId = await ResolveTeamActiveProjectIdAsync(teamId, cancellationToken);
        if (!activeProjectId.HasValue) return recs;
        return recs.Where(r => r.ProjectId == activeProjectId.Value || r.Status != TeamRecommendationStatus.PENDING).ToList();
    }

    public async Task<IReadOnlyCollection<TeamProjectRecommendationResponse>> ListRecommendationsForStudentAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var teamIds = await dbContext.TeamMembers.AsNoTracking()
            .Where(m => m.StudentId == studentId)
            .Select(m => m.TeamId)
            .ToListAsync(cancellationToken);
        if (teamIds.Count == 0) return [];
        var recs = await ProjectRecommendationsAsync(dbContext.TeamProjectRecommendations.AsNoTracking().Where(r => teamIds.Contains(r.TeamId)), cancellationToken);
        var activeByTeam = await ResolveTeamsActiveProjectIdsAsync(teamIds, cancellationToken);
        return recs.Where(r =>
        {
            if (!activeByTeam.TryGetValue(r.TeamId, out var activeProjId)) return true;
            return r.ProjectId == activeProjId || r.Status != TeamRecommendationStatus.PENDING;
        }).ToList();
    }

    public Task<TeamProjectRecommendation?> FindRecommendationForUpdateAsync(Guid recommendationId, CancellationToken cancellationToken) =>
        dbContext.TeamProjectRecommendations.AsTracking().SingleOrDefaultAsync(r => r.Id == recommendationId, cancellationToken);

    public Task<Guid?> FindLecturerUserIdAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles.AsNoTracking().Where(l => l.Id == lecturerId).Select(l => (Guid?)l.UserId).SingleOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyCollection<TeamResponse>> ProjectTeamsByIdsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        var teams = await dbContext.Teams.AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        if (teams.Count == 0) return [];

        var membersRaw = await (
            from m in dbContext.TeamMembers.AsNoTracking()
            join sp in dbContext.StudentProfiles.AsNoTracking() on m.StudentId equals sp.Id
            join u in dbContext.Users.AsNoTracking() on sp.UserId equals u.Id
            where teamIds.Contains(m.TeamId)
            orderby m.Role, m.JoinedAt
            select new
            {
                m.TeamId,
                m.StudentId,
                m.Role,
                m.FunctionalRole,
                m.JoinedAt,
                sp.UserId,
                u.DisplayName,
                sp.StudentCode,
                u.Email,
                sp.AvatarUrl,
                sp.AcademicYear,
                MajorName = sp.MajorId.HasValue ? dbContext.Majors.Where(x => x.Id == sp.MajorId.Value).Select(x => x.Name).FirstOrDefault() : null,
                FacultyName = sp.FacultyId.HasValue ? dbContext.Faculties.Where(x => x.Id == sp.FacultyId.Value).Select(x => x.Name).FirstOrDefault() : null
            }).ToListAsync(cancellationToken);

        var studentIds = membersRaw.Select(m => m.StudentId).Distinct().ToList();
        var skillsRows = studentIds.Count == 0 ? [] : await dbContext.StudentSkills.AsNoTracking()
            .Where(s => studentIds.Contains(s.StudentId))
            .Join(dbContext.Skills.AsNoTracking(), s => s.SkillId, sk => sk.Id, (s, sk) => new { s.StudentId, sk.Name })
            .ToListAsync(cancellationToken);
        var skillsByStudent = skillsRows.GroupBy(x => x.StudentId).ToDictionary(g => g.Key, g => (IReadOnlyCollection<string>)g.Select(x => x.Name).Distinct().Take(6).ToList());

        var activeApps = await dbContext.Applications.AsNoTracking()
            .Where(a => a.TeamId.HasValue &&
                        teamIds.Contains(a.TeamId.Value) &&
                        ActiveTeamAppStatuses.Contains(a.Status) &&
                        a.Project.Status != ProjectStatus.COMPLETED &&
                        a.Project.Status != ProjectStatus.CANCELLED)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                TeamId = a.TeamId!.Value,
                a.ProjectId,
                a.Status,
                ProjectTitle = a.Project.Title,
                ProjectSlug = a.Project.Slug,
                ProjectStatus = a.Project.Status
            })
            .ToListAsync(cancellationToken);

        var targetProjectIds = teams.Where(t => t.TargetProjectId.HasValue).Select(t => t.TargetProjectId!.Value).Distinct().ToList();
        var targetProjects = targetProjectIds.Count == 0
            ? new Dictionary<Guid, (Guid Id, string Title, string Slug, ProjectStatus Status)>()
            : (await dbContext.Projects.AsNoTracking()
                .Where(p => targetProjectIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Title, p.Slug, p.Status })
                .ToListAsync(cancellationToken))
                .ToDictionary(p => p.Id, p => (p.Id, p.Title, p.Slug, p.Status));

        return teams.Select(t =>
        {
            var members = membersRaw.Where(m => m.TeamId == t.Id)
                .Select(m => new TeamMemberResponse(
                    m.StudentId,
                    m.Role,
                    m.UserId,
                    m.DisplayName,
                    m.StudentCode,
                    m.Email,
                    m.AvatarUrl,
                    m.MajorName,
                    m.FacultyName,
                    m.AcademicYear,
                    m.FunctionalRole,
                    skillsByStudent.GetValueOrDefault(m.StudentId, []),
                    m.JoinedAt))
                .ToList();

            var leader = members.FirstOrDefault(m => m.Role == TeamMemberRole.LEADER);
            var teamApps = activeApps.Where(a => a.TeamId == t.Id).ToList();
            var acceptedApp = teamApps.FirstOrDefault(a => a.Status == ApplicationStatus.ACCEPTED);
            var inFlightApp = teamApps.FirstOrDefault(a => a.Status != ApplicationStatus.ACCEPTED);

            (Guid Id, string Title, string Slug, ProjectStatus Status)? unfinishedTargetProj = null;
            if (t.TargetProjectId.HasValue &&
                targetProjects.TryGetValue(t.TargetProjectId.Value, out var targetProjRaw) &&
                targetProjRaw.Status != ProjectStatus.COMPLETED &&
                targetProjRaw.Status != ProjectStatus.CANCELLED)
            {
                unfinishedTargetProj = targetProjRaw;
            }

            Guid? activeProjectId = null;
            string? activeProjectTitle = null;
            string? activeProjectSlug = null;
            string? activeProjectStatus = null;
            string? activeProjectPhase = null;
            bool hasActiveProject = false;
            bool effectiveIsLocked = false;

            if (acceptedApp is not null)
            {
                activeProjectId = acceptedApp.ProjectId;
                activeProjectTitle = acceptedApp.ProjectTitle;
                activeProjectSlug = acceptedApp.ProjectSlug;
                activeProjectStatus = acceptedApp.ProjectStatus.ToString();
                activeProjectPhase = "IN_PROGRESS";
                hasActiveProject = true;
                effectiveIsLocked = true;
            }
            else if (t.IsLocked && unfinishedTargetProj is { } lockedTarget)
            {
                activeProjectId = lockedTarget.Id;
                activeProjectTitle = lockedTarget.Title;
                activeProjectSlug = lockedTarget.Slug;
                activeProjectStatus = lockedTarget.Status.ToString();
                activeProjectPhase = "IN_PROGRESS";
                hasActiveProject = true;
                effectiveIsLocked = true;
            }
            else if (inFlightApp is not null)
            {
                activeProjectId = inFlightApp.ProjectId;
                activeProjectTitle = inFlightApp.ProjectTitle;
                activeProjectSlug = inFlightApp.ProjectSlug;
                activeProjectStatus = inFlightApp.ProjectStatus.ToString();
                activeProjectPhase = "APPLYING";
                hasActiveProject = true;
                effectiveIsLocked = false;
            }

            var displayTargetId = activeProjectId ?? unfinishedTargetProj?.Id;
            var displayTargetTitle = activeProjectTitle ?? unfinishedTargetProj?.Title;

            return new TeamResponse(
                t.Id,
                t.Name,
                t.CreatedByStudentId,
                effectiveIsLocked,
                members,
                t.CreatedAt,
                t.Description,
                t.TeamType,
                displayTargetId,
                displayTargetTitle,
                leader?.DisplayName,
                t.AvatarUrl,
                activeProjectId,
                activeProjectTitle,
                activeProjectSlug,
                activeProjectStatus,
                activeProjectPhase,
                hasActiveProject);
        }).ToList();
    }

    private async Task<Guid?> ResolveTeamActiveProjectIdAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var map = await ResolveTeamsActiveProjectIdsAsync([teamId], cancellationToken);
        return map.TryGetValue(teamId, out var projectId) ? projectId : null;
    }

    private async Task<Dictionary<Guid, Guid>> ResolveTeamsActiveProjectIdsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, Guid>();
        if (teamIds.Count == 0) return result;

        var activeApps = await dbContext.Applications.AsNoTracking()
            .Where(a => a.TeamId.HasValue &&
                        teamIds.Contains(a.TeamId.Value) &&
                        ActiveTeamAppStatuses.Contains(a.Status) &&
                        a.Project.Status != ProjectStatus.COMPLETED &&
                        a.Project.Status != ProjectStatus.CANCELLED)
            .OrderByDescending(a => a.Status == ApplicationStatus.ACCEPTED)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new { TeamId = a.TeamId!.Value, a.ProjectId })
            .ToListAsync(cancellationToken);

        foreach (var app in activeApps)
        {
            result.TryAdd(app.TeamId, app.ProjectId);
        }

        var lockedTeams = await dbContext.Teams.AsNoTracking()
            .Where(t => teamIds.Contains(t.Id) &&
                        t.IsLocked &&
                        t.TargetProjectId.HasValue &&
                        dbContext.Projects.Any(p => p.Id == t.TargetProjectId.Value &&
                                                    p.Status != ProjectStatus.COMPLETED &&
                                                    p.Status != ProjectStatus.CANCELLED))
            .Select(t => new { t.Id, ProjectId = t.TargetProjectId!.Value })
            .ToListAsync(cancellationToken);

        foreach (var lt in lockedTeams)
        {
            result.TryAdd(lt.Id, lt.ProjectId);
        }

        return result;
    }

    private async Task<IReadOnlyCollection<TeamInvitationResponse>> ProjectInvitationsQuery(IQueryable<TeamInvitation> query, CancellationToken cancellationToken)
    {
        var rows = await (
            from inv in query
            join team in dbContext.Teams.AsNoTracking() on inv.TeamId equals team.Id
            join invitedSp in dbContext.StudentProfiles.AsNoTracking() on inv.InvitedStudentId equals invitedSp.Id
            join invitedUser in dbContext.Users.AsNoTracking() on invitedSp.UserId equals invitedUser.Id
            join inviterSp in dbContext.StudentProfiles.AsNoTracking() on inv.InvitedByStudentId equals inviterSp.Id
            join inviterUser in dbContext.Users.AsNoTracking() on inviterSp.UserId equals inviterUser.Id
            select new TeamInvitationResponse(
                inv.Id,
                inv.TeamId,
                team.Name,
                inv.InvitedStudentId,
                inv.InvitedByStudentId,
                inv.Role,
                inv.Status,
                inv.ExpiresAt,
                inv.CreatedAt,
                team.Description,
                team.TeamType,
                invitedUser.DisplayName,
                invitedSp.StudentCode,
                invitedUser.Email,
                inviterUser.DisplayName,
                inv.FunctionalRole,
                inv.Message,
                inv.RespondedAt,
                team.AvatarUrl))
            .ToListAsync(cancellationToken);

        return rows;
    }

    private async Task<IReadOnlyCollection<TeamProjectRecommendationResponse>> ProjectRecommendationsAsync(IQueryable<TeamProjectRecommendation> query, CancellationToken cancellationToken)
    {
        return await (
            from rec in query
            join lec in dbContext.LecturerProfiles.AsNoTracking() on rec.LecturerId equals lec.Id
            join lecUser in dbContext.Users.AsNoTracking() on lec.UserId equals lecUser.Id
            join team in dbContext.Teams.AsNoTracking() on rec.TeamId equals team.Id
            join proj in dbContext.Projects.AsNoTracking() on rec.ProjectId equals proj.Id
            join comp in dbContext.Companies.AsNoTracking() on proj.CompanyId equals comp.Id
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
}
