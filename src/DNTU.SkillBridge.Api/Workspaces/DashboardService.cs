using System.Text.Json;
using DNTU.SkillBridge.Domain.Applications;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Meetings;
using DNTU.SkillBridge.Domain.Milestones;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Workspaces;

/// <summary>Server-side aggregate dashboard queries and deterministic risk snapshot refreshes.</summary>
public sealed class DashboardService(AppDbContext dbContext)
{
    public async Task<ProjectProgressResponse?> GetProjectProgressAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!await CanReadAsync(userId, projectId, cancellationToken)) return null;
        return await BuildProgressAsync(projectId, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task<StudentDashboardResponse?> GetStudentDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await dbContext.StudentProfiles.AsNoTracking().Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (studentId is null) return null;
        var now = DateTimeOffset.UtcNow;
        var projectRows = await dbContext.Projects.AsNoTracking()
            .Where(p => dbContext.ProjectMembers.Any(m => m.ProjectId == p.Id && m.StudentId == studentId && m.IsActive))
            .OrderByDescending(p => p.UpdatedAt).Select(p => new ProjectRow(p.Id, p.Title, p.Status)).ToListAsync(cancellationToken);
        var projects = await BuildDashboardProjectsAsync(projectRows, now, cancellationToken);
        var assignedTasks = await dbContext.ProjectTasks.AsNoTracking().CountAsync(t => t.AssigneeStudentId == studentId && !t.IsDeleted && t.Status != ProjectTaskStatus.DONE, cancellationToken);
        var upcomingMeetings = await dbContext.MeetingParticipants.AsNoTracking()
            .Join(dbContext.ProjectMeetings.AsNoTracking(), participant => participant.MeetingId, meeting => meeting.Id, (participant, meeting) => new { participant, meeting })
            .CountAsync(x => x.participant.StudentId == studentId && x.meeting.StartAt >= now, cancellationToken);
        var revisions = await dbContext.ProjectSubmissions.AsNoTracking().CountAsync(x => x.SubmittedByStudentId == studentId && x.Status == SubmissionStatus.REVISION_REQUIRED, cancellationToken);
        var unread = await dbContext.Notifications.AsNoTracking().CountAsync(x => x.UserId == userId && x.ReadAt == null, cancellationToken);
        return new StudentDashboardResponse(projects, assignedTasks, upcomingMeetings, revisions, unread);
    }

    public async Task<CompanyDashboardResponse?> GetCompanyDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var companyId = await dbContext.CompanyMembers.AsNoTracking().Where(x => x.UserId == userId).Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(cancellationToken);
        if (companyId is null) return null;
        var now = DateTimeOffset.UtcNow;
        var baseQuery = dbContext.Projects.AsNoTracking().Where(p => p.CompanyId == companyId && p.IsActive);
        var rows = await baseQuery.OrderByDescending(p => p.UpdatedAt).Select(p => new ProjectRow(p.Id, p.Title, p.Status)).ToListAsync(cancellationToken);
        var projects = await BuildDashboardProjectsAsync(rows, now, cancellationToken);
        var ids = rows.Select(x => x.Id).ToArray();
        var active = rows.Count(x => x.Status == ProjectStatus.IN_PROGRESS);
        var pending = rows.Count(x => x.Status == ProjectStatus.PENDING_APPROVAL);
        var recruiting = rows.Count(x => x.Status == ProjectStatus.RECRUITING);
        var applications = await dbContext.Applications.AsNoTracking().CountAsync(x => ids.Contains(x.ProjectId) && x.Status == ApplicationStatus.PENDING, cancellationToken);
        var submissions = await dbContext.ProjectSubmissions.AsNoTracking().CountAsync(x => ids.Contains(x.ProjectId) && (x.Status == SubmissionStatus.SUBMITTED || x.Status == SubmissionStatus.RESUBMITTED), cancellationToken);
        var meetings = await dbContext.ProjectMeetings.AsNoTracking().CountAsync(x => ids.Contains(x.ProjectId) && x.StartAt >= now, cancellationToken);
        return new CompanyDashboardResponse(active, pending, recruiting, applications, submissions, meetings, projects);
    }

    public async Task<LecturerDashboardResponse?> GetLecturerDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var lecturerId = await dbContext.LecturerProfiles.AsNoTracking().Where(x => x.UserId == userId && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (lecturerId is null) return null;
        var now = DateTimeOffset.UtcNow;
        var rows = await dbContext.LecturerAssignments.AsNoTracking().Where(x => x.LecturerId == lecturerId && x.Status == LecturerAssignmentStatus.ACTIVE)
            .Join(dbContext.Projects.AsNoTracking(), x => x.ProjectId, p => p.Id, (_, p) => new ProjectRow(p.Id, p.Title, p.Status)).ToListAsync(cancellationToken);
        var projects = await BuildDashboardProjectsAsync(rows, now, cancellationToken);
        var ids = rows.Select(x => x.Id).ToArray();
        var riskProjects = projects.Where(x => x.Progress.Risk.Level != ProjectRiskLevel.LOW).ToList();
        var overdueMilestones = await dbContext.ProjectMilestones.AsNoTracking().CountAsync(x => ids.Contains(x.ProjectId) && x.DueAt < now && x.Status != MilestoneStatus.APPROVED, cancellationToken);
        var submissions = await dbContext.ProjectSubmissions.AsNoTracking().CountAsync(x => ids.Contains(x.ProjectId) && (x.Status == SubmissionStatus.SUBMITTED || x.Status == SubmissionStatus.RESUBMITTED), cancellationToken);
        var meetings = await dbContext.ProjectMeetings.AsNoTracking().CountAsync(x => ids.Contains(x.ProjectId) && x.StartAt >= now, cancellationToken);
        var activity = await dbContext.ProjectActivities.AsNoTracking().CountAsync(x => ids.Contains(x.ProjectId) && x.CreatedAt >= now.AddDays(-7), cancellationToken);
        return new LecturerDashboardResponse(riskProjects, overdueMilestones, submissions, meetings, 0, activity);
    }

    private async Task<IReadOnlyCollection<DashboardProjectResponse>> BuildDashboardProjectsAsync(IReadOnlyCollection<ProjectRow> rows, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return [];
        var projectIds = rows.Select(x => x.Id).ToArray();
        // Fixed-count grouped projections avoid an overview query per project (no N+1).
        var tasks = await dbContext.ProjectTasks.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId) && !x.IsDeleted)
            .GroupBy(x => x.ProjectId).Select(g => new { ProjectId = g.Key, Total = g.Count(), Done = g.Count(x => x.Status == ProjectTaskStatus.DONE), Overdue = g.Count(x => x.Status != ProjectTaskStatus.DONE && x.DueAt < now), OldOverdue = g.Count(x => x.Status != ProjectTaskStatus.DONE && x.DueAt < now.AddDays(-3)) }).ToListAsync(cancellationToken);
        var milestones = await dbContext.ProjectMilestones.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId))
            .GroupBy(x => x.ProjectId).Select(g => new { ProjectId = g.Key, Total = g.Count(), Approved = g.Count(x => x.Status == MilestoneStatus.APPROVED), Overdue = g.Count(x => x.Status != MilestoneStatus.APPROVED && x.DueAt < now) }).ToListAsync(cancellationToken);
        var revisions = await dbContext.ProjectSubmissions.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId) && (x.Status == SubmissionStatus.REVISION_REQUIRED || x.CurrentVersionNumber >= 3))
            .GroupBy(x => x.ProjectId).Select(g => new { ProjectId = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var missed = await dbContext.MeetingParticipants.AsNoTracking().Join(dbContext.ProjectMeetings.AsNoTracking(), participant => participant.MeetingId, meeting => meeting.Id, (participant, meeting) => new { participant, meeting })
            .Where(x => projectIds.Contains(x.meeting.ProjectId) && x.participant.AttendanceStatus == MeetingAttendanceStatus.ABSENT && x.meeting.StartAt < now)
            .GroupBy(x => x.meeting.ProjectId).Select(g => new { ProjectId = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var taskMap = tasks.ToDictionary(x => x.ProjectId);
        var milestoneMap = milestones.ToDictionary(x => x.ProjectId);
        var revisionMap = revisions.ToDictionary(x => x.ProjectId, x => x.Count);
        var missedMap = missed.ToDictionary(x => x.ProjectId, x => x.Count);
        return rows.Select(row =>
        {
            taskMap.TryGetValue(row.Id, out var task);
            milestoneMap.TryGetValue(row.Id, out var milestone);
            var assessment = ProjectRiskCalculator.Calculate(milestone?.Overdue ?? 0, task?.OldOverdue ?? 0, revisionMap.GetValueOrDefault(row.Id), missedMap.GetValueOrDefault(row.Id));
            var totalWork = (task?.Total ?? 0) + (milestone?.Total ?? 0);
            var completedWork = (task?.Done ?? 0) + (milestone?.Approved ?? 0);
            var completion = totalWork == 0 ? 0 : Math.Round(completedWork * 100m / totalWork, 2);
            var risk = new ProjectRiskResponse(assessment.Score, assessment.Level, assessment.Reasons, now);
            return new DashboardProjectResponse(row.Id, row.Title, row.Status.ToString(), new ProjectProgressResponse(row.Id, task?.Total ?? 0, task?.Done ?? 0, task?.Overdue ?? 0, milestone?.Total ?? 0, milestone?.Approved ?? 0, milestone?.Overdue ?? 0, completion, risk));
        }).ToList();
    }

    private async Task<ProjectProgressResponse> BuildProgressAsync(Guid projectId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var totals = await dbContext.ProjectTasks.AsNoTracking().Where(x => x.ProjectId == projectId && !x.IsDeleted)
            .GroupBy(_ => 1).Select(g => new { Total = g.Count(), Done = g.Count(x => x.Status == ProjectTaskStatus.DONE), Overdue = g.Count(x => x.Status != ProjectTaskStatus.DONE && x.DueAt < now), OldOverdue = g.Count(x => x.Status != ProjectTaskStatus.DONE && x.DueAt < now.AddDays(-3)) }).SingleOrDefaultAsync(cancellationToken);
        var milestones = await dbContext.ProjectMilestones.AsNoTracking().Where(x => x.ProjectId == projectId)
            .GroupBy(_ => 1).Select(g => new { Total = g.Count(), Approved = g.Count(x => x.Status == MilestoneStatus.APPROVED), Overdue = g.Count(x => x.Status != MilestoneStatus.APPROVED && x.DueAt < now) }).SingleOrDefaultAsync(cancellationToken);
        var revisions = await dbContext.ProjectSubmissions.AsNoTracking().Where(x => x.ProjectId == projectId).CountAsync(x => x.Status == SubmissionStatus.REVISION_REQUIRED || x.CurrentVersionNumber >= 3, cancellationToken);
        var missed = await dbContext.MeetingParticipants.AsNoTracking()
            .Join(dbContext.ProjectMeetings.AsNoTracking(), participant => participant.MeetingId, meeting => meeting.Id, (participant, meeting) => new { participant, meeting })
            .CountAsync(x => x.participant.AttendanceStatus == MeetingAttendanceStatus.ABSENT && x.meeting.ProjectId == projectId && x.meeting.StartAt < now, cancellationToken);
        var assessment = ProjectRiskCalculator.Calculate(milestones?.Overdue ?? 0, totals?.OldOverdue ?? 0, revisions, missed);
        var risk = new ProjectRiskResponse(assessment.Score, assessment.Level, assessment.Reasons, now);
        var totalWork = (totals?.Total ?? 0) + (milestones?.Total ?? 0);
        var completedWork = (totals?.Done ?? 0) + (milestones?.Approved ?? 0);
        var completion = totalWork == 0 ? 0 : Math.Round(completedWork * 100m / totalWork, 2);
        return new ProjectProgressResponse(projectId, totals?.Total ?? 0, totals?.Done ?? 0, totals?.Overdue ?? 0, milestones?.Total ?? 0, milestones?.Approved ?? 0, milestones?.Overdue ?? 0, completion, risk);
    }

    private async Task<ProjectRiskResponse> RefreshRiskAsync(Guid projectId, ProjectRiskAssessment assessment, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reasonsJson = JsonSerializer.Serialize(assessment.Reasons);
        var snapshot = await dbContext.ProjectRiskSnapshots.SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);
        if (snapshot is null)
        {
            snapshot = new ProjectRiskSnapshot(projectId, assessment.Score, assessment.Level, reasonsJson, now);
            dbContext.ProjectRiskSnapshots.Add(snapshot);
        }
        else snapshot.Apply(assessment.Score, assessment.Level, reasonsJson, now);
        dbContext.ProjectRiskHistory.Add(new ProjectRiskHistory(projectId, assessment.Score, assessment.Level, reasonsJson, now));
        dbContext.ProjectActivities.Add(new ProjectActivity(projectId, "PROJECT_RISK_REFRESHED", null, reasonsJson));
        dbContext.OutboxMessages.Add(new OutboxMessage("project.risk.refreshed", JsonSerializer.Serialize(new { projectId, assessment.Score, assessment.Level, calculatedAt = now })));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ProjectRiskResponse(assessment.Score, assessment.Level, assessment.Reasons, now);
    }

    private async Task<bool> CanReadAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers.AsNoTracking().AnyAsync(x => x.ProjectId == projectId && x.IsActive && x.Student.UserId == userId, cancellationToken) ||
        await dbContext.Projects.AsNoTracking().AnyAsync(x => x.Id == projectId && x.Company.Members.Any(m => m.UserId == userId), cancellationToken) ||
        await dbContext.LecturerAssignments.AsNoTracking().Join(dbContext.LecturerProfiles.AsNoTracking(), a => a.LecturerId, l => l.Id, (a, l) => new { a, l }).AnyAsync(x => x.a.ProjectId == projectId && x.a.Status == LecturerAssignmentStatus.ACTIVE && x.l.UserId == userId, cancellationToken) ||
        await dbContext.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.UserRoles.Any(r => r.Role.NormalizedName == RoleNames.Admin || r.Role.NormalizedName == RoleNames.SuperAdmin), cancellationToken);

    private sealed record ProjectRow(Guid Id, string Title, ProjectStatus Status);
}
