using DNTU.SkillBridge.Application.Workspaces;
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

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the dashboard data operations.</summary>
public sealed class DashboardRepository(AppDbContext dbContext) : IDashboardRepository
{
    public Task<Guid?> FindStudentIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles.AsNoTracking().Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<DashboardProjectRow>> ListStudentProjectRowsAsync(Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.Projects.AsNoTracking()
            .Where(p => dbContext.ProjectMembers.Any(m => m.ProjectId == p.Id && m.StudentId == studentId && m.IsActive))
            .OrderByDescending(p => p.UpdatedAt).Select(p => new DashboardProjectRow(p.Id, p.Title, p.Status)).ToListAsync(cancellationToken);

    public Task<int> CountAssignedTasksAsync(Guid studentId, CancellationToken cancellationToken) =>
        dbContext.ProjectTasks.AsNoTracking().CountAsync(t => t.AssigneeStudentId == studentId && !t.IsDeleted && t.Status != ProjectTaskStatus.DONE, cancellationToken);

    public Task<int> CountUpcomingMeetingsAsync(Guid studentId, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.MeetingParticipants.AsNoTracking()
            .Join(dbContext.ProjectMeetings.AsNoTracking(), participant => participant.MeetingId, meeting => meeting.Id, (participant, meeting) => new { participant, meeting })
            .CountAsync(x => x.participant.StudentId == studentId && x.meeting.StartAt >= now, cancellationToken);

    public Task<int> CountRevisionSubmissionsAsync(Guid studentId, CancellationToken cancellationToken) =>
        dbContext.ProjectSubmissions.AsNoTracking().CountAsync(x => x.SubmittedByStudentId == studentId && x.Status == SubmissionStatus.REVISION_REQUIRED, cancellationToken);

    public Task<int> CountUnreadNotificationsAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Notifications.AsNoTracking().CountAsync(x => x.UserId == userId && x.ReadAt == null, cancellationToken);

    public Task<Guid?> FindCompanyIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.CompanyMembers.AsNoTracking().Where(x => x.UserId == userId).Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<DashboardProjectRow>> ListCompanyProjectRowsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var baseQuery = dbContext.Projects.AsNoTracking().Where(p => p.CompanyId == companyId && p.IsActive);
        return await baseQuery.OrderByDescending(p => p.UpdatedAt).Select(p => new DashboardProjectRow(p.Id, p.Title, p.Status)).ToListAsync(cancellationToken);
    }

    public Task<int> CountPendingApplicationsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        dbContext.Applications.AsNoTracking().CountAsync(x => projectIds.Contains(x.ProjectId) && x.Status == ApplicationStatus.PENDING, cancellationToken);

    public Task<int> CountWaitingSubmissionsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        dbContext.ProjectSubmissions.AsNoTracking().CountAsync(x => projectIds.Contains(x.ProjectId) && (x.Status == SubmissionStatus.SUBMITTED || x.Status == SubmissionStatus.RESUBMITTED), cancellationToken);

    public Task<int> CountUpcomingProjectMeetingsAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.ProjectMeetings.AsNoTracking().CountAsync(x => projectIds.Contains(x.ProjectId) && x.StartAt >= now, cancellationToken);

    public Task<Guid?> FindLecturerIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles.AsNoTracking().Where(x => x.UserId == userId && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<DashboardProjectRow>> ListLecturerProjectRowsAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        await dbContext.LecturerAssignments.AsNoTracking().Where(x => x.LecturerId == lecturerId && x.Status == LecturerAssignmentStatus.ACTIVE)
            .Join(dbContext.Projects.AsNoTracking(), x => x.ProjectId, p => p.Id, (_, p) => new DashboardProjectRow(p.Id, p.Title, p.Status)).ToListAsync(cancellationToken);

    public Task<int> CountOverdueMilestonesAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.ProjectMilestones.AsNoTracking().CountAsync(x => projectIds.Contains(x.ProjectId) && x.DueAt < now && x.Status != MilestoneStatus.APPROVED, cancellationToken);

    public Task<int> CountRecentProjectActivityAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.ProjectActivities.AsNoTracking().CountAsync(x => projectIds.Contains(x.ProjectId) && x.CreatedAt >= now.AddDays(-7), cancellationToken);

    public async Task<IReadOnlyCollection<DashboardTaskGroupRow>> ListTaskGroupsAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.ProjectTasks.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId) && !x.IsDeleted)
            .GroupBy(x => x.ProjectId).Select(g => new DashboardTaskGroupRow(g.Key, g.Count(), g.Count(x => x.Status == ProjectTaskStatus.DONE), g.Count(x => x.Status != ProjectTaskStatus.DONE && x.DueAt < now), g.Count(x => x.Status != ProjectTaskStatus.DONE && x.DueAt < now.AddDays(-3)))).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<DashboardMilestoneGroupRow>> ListMilestoneGroupsAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.ProjectMilestones.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId))
            .GroupBy(x => x.ProjectId).Select(g => new DashboardMilestoneGroupRow(g.Key, g.Count(), g.Count(x => x.Status == MilestoneStatus.APPROVED), g.Count(x => x.Status != MilestoneStatus.APPROVED && x.DueAt < now))).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<DashboardCountGroupRow>> ListRevisionGroupsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        await dbContext.ProjectSubmissions.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId) && (x.Status == SubmissionStatus.REVISION_REQUIRED || x.CurrentVersionNumber >= 3))
            .GroupBy(x => x.ProjectId).Select(g => new DashboardCountGroupRow(g.Key, g.Count())).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<DashboardCountGroupRow>> ListMissedMeetingGroupsAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.MeetingParticipants.AsNoTracking().Join(dbContext.ProjectMeetings.AsNoTracking(), participant => participant.MeetingId, meeting => meeting.Id, (participant, meeting) => new { participant, meeting })
            .Where(x => projectIds.Contains(x.meeting.ProjectId) && x.participant.AttendanceStatus == MeetingAttendanceStatus.ABSENT && x.meeting.StartAt < now)
            .GroupBy(x => x.meeting.ProjectId).Select(g => new DashboardCountGroupRow(g.Key, g.Count())).ToListAsync(cancellationToken);

    public async Task<DashboardTaskTotalsRow?> GetTaskTotalsAsync(Guid projectId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.ProjectTasks.AsNoTracking().Where(x => x.ProjectId == projectId && !x.IsDeleted)
            .GroupBy(_ => 1).Select(g => new DashboardTaskTotalsRow(g.Count(), g.Count(x => x.Status == ProjectTaskStatus.DONE), g.Count(x => x.Status != ProjectTaskStatus.DONE && x.DueAt < now), g.Count(x => x.Status != ProjectTaskStatus.DONE && x.DueAt < now.AddDays(-3)))).SingleOrDefaultAsync(cancellationToken);

    public async Task<DashboardMilestoneTotalsRow?> GetMilestoneTotalsAsync(Guid projectId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.ProjectMilestones.AsNoTracking().Where(x => x.ProjectId == projectId)
            .GroupBy(_ => 1).Select(g => new DashboardMilestoneTotalsRow(g.Count(), g.Count(x => x.Status == MilestoneStatus.APPROVED), g.Count(x => x.Status != MilestoneStatus.APPROVED && x.DueAt < now))).SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountProjectRevisionSubmissionsAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectSubmissions.AsNoTracking().Where(x => x.ProjectId == projectId).CountAsync(x => x.Status == SubmissionStatus.REVISION_REQUIRED || x.CurrentVersionNumber >= 3, cancellationToken);

    public Task<int> CountMissedMeetingsAsync(Guid projectId, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.MeetingParticipants.AsNoTracking()
            .Join(dbContext.ProjectMeetings.AsNoTracking(), participant => participant.MeetingId, meeting => meeting.Id, (participant, meeting) => new { participant, meeting })
            .CountAsync(x => x.participant.AttendanceStatus == MeetingAttendanceStatus.ABSENT && x.meeting.ProjectId == projectId && x.meeting.StartAt < now, cancellationToken);

    public Task<ProjectRiskSnapshot?> FindRiskSnapshotAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectRiskSnapshots.SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

    public void AddRiskSnapshot(ProjectRiskSnapshot snapshot) => dbContext.ProjectRiskSnapshots.Add(snapshot);

    public void AddRiskHistory(ProjectRiskHistory history) => dbContext.ProjectRiskHistory.Add(history);

    public void AddProjectActivity(ProjectActivity activity) => dbContext.ProjectActivities.Add(activity);

    public void AddOutboxMessage(OutboxMessage message) => dbContext.OutboxMessages.Add(message);

    public async Task<bool> CanReadAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers.AsNoTracking().AnyAsync(x => x.ProjectId == projectId && x.IsActive && x.Student.UserId == userId, cancellationToken) ||
        await dbContext.Projects.AsNoTracking().AnyAsync(x => x.Id == projectId && x.Company.Members.Any(m => m.UserId == userId), cancellationToken) ||
        await dbContext.LecturerAssignments.AsNoTracking().Join(dbContext.LecturerProfiles.AsNoTracking(), a => a.LecturerId, l => l.Id, (a, l) => new { a, l }).AnyAsync(x => x.a.ProjectId == projectId && x.a.Status == LecturerAssignmentStatus.ACTIVE && x.l.UserId == userId, cancellationToken) ||
        await dbContext.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.UserRoles.Any(r => r.Role.NormalizedName == RoleNames.Admin || r.Role.NormalizedName == RoleNames.SuperAdmin), cancellationToken);
}
