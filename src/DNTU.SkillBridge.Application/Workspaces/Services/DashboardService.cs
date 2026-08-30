using System.Text.Json;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

/// <summary>Server-side aggregate dashboard queries and deterministic risk snapshot refreshes.</summary>
public sealed class DashboardService(IDashboardRepository dashboardRepository, IUnitOfWork unitOfWork) : IDashboardService
{
    public async Task<ProjectProgressResponse?> GetProjectProgressAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!await dashboardRepository.CanReadAsync(userId, projectId, cancellationToken)) return null;
        return await BuildProgressAsync(projectId, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task<StudentDashboardResponse?> GetStudentDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await dashboardRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return null;
        var now = DateTimeOffset.UtcNow;
        var rows = await dashboardRepository.ListStudentProjectRowsAsync(studentId.Value, cancellationToken);
        var projects = await BuildDashboardProjectsAsync(rows, now, cancellationToken);
        var assignedTasks = await dashboardRepository.CountAssignedTasksAsync(studentId.Value, cancellationToken);
        var upcomingMeetings = await dashboardRepository.CountUpcomingMeetingsAsync(studentId.Value, now, cancellationToken);
        var revisions = await dashboardRepository.CountRevisionSubmissionsAsync(studentId.Value, cancellationToken);
        var unread = await dashboardRepository.CountUnreadNotificationsAsync(userId, cancellationToken);
        return new StudentDashboardResponse(projects, assignedTasks, upcomingMeetings, revisions, unread);
    }

    public async Task<CompanyDashboardResponse?> GetCompanyDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var companyId = await dashboardRepository.FindCompanyIdAsync(userId, cancellationToken);
        if (companyId is null) return null;
        var now = DateTimeOffset.UtcNow;
        var rows = await dashboardRepository.ListCompanyProjectRowsAsync(companyId.Value, cancellationToken);
        var projects = await BuildDashboardProjectsAsync(rows, now, cancellationToken);
        var ids = rows.Select(x => x.Id).ToArray();
        var active = rows.Count(x => x.Status == ProjectStatus.IN_PROGRESS);
        var pending = rows.Count(x => x.Status == ProjectStatus.PENDING_APPROVAL);
        var recruiting = rows.Count(x => x.Status == ProjectStatus.RECRUITING);
        var applications = await dashboardRepository.CountPendingApplicationsAsync(ids, cancellationToken);
        var submissions = await dashboardRepository.CountWaitingSubmissionsAsync(ids, cancellationToken);
        var meetings = await dashboardRepository.CountUpcomingProjectMeetingsAsync(ids, now, cancellationToken);
        return new CompanyDashboardResponse(active, pending, recruiting, applications, submissions, meetings, projects);
    }

    public async Task<LecturerDashboardResponse?> GetLecturerDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var lecturerId = await dashboardRepository.FindLecturerIdAsync(userId, cancellationToken);
        if (lecturerId is null) return null;
        var now = DateTimeOffset.UtcNow;
        var rows = await dashboardRepository.ListLecturerProjectRowsAsync(lecturerId.Value, cancellationToken);
        var projects = await BuildDashboardProjectsAsync(rows, now, cancellationToken);
        var ids = rows.Select(x => x.Id).ToArray();
        var riskProjects = projects.Where(x => x.Progress.Risk.Level != ProjectRiskLevel.LOW).ToList();
        var overdueMilestones = await dashboardRepository.CountOverdueMilestonesAsync(ids, now, cancellationToken);
        var submissions = await dashboardRepository.CountWaitingSubmissionsAsync(ids, cancellationToken);
        var meetings = await dashboardRepository.CountUpcomingProjectMeetingsAsync(ids, now, cancellationToken);
        var activity = await dashboardRepository.CountRecentProjectActivityAsync(ids, now, cancellationToken);
        return new LecturerDashboardResponse(riskProjects, overdueMilestones, submissions, meetings, 0, activity);
    }

    private async Task<IReadOnlyCollection<DashboardProjectResponse>> BuildDashboardProjectsAsync(IReadOnlyCollection<DashboardProjectRow> rows, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return [];
        var projectIds = rows.Select(x => x.Id).ToArray();
        // Fixed-count grouped projections avoid an overview query per project (no N+1).
        var tasks = await dashboardRepository.ListTaskGroupsAsync(projectIds, now, cancellationToken);
        var milestones = await dashboardRepository.ListMilestoneGroupsAsync(projectIds, now, cancellationToken);
        var revisions = await dashboardRepository.ListRevisionGroupsAsync(projectIds, cancellationToken);
        var missed = await dashboardRepository.ListMissedMeetingGroupsAsync(projectIds, now, cancellationToken);
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
        var totals = await dashboardRepository.GetTaskTotalsAsync(projectId, now, cancellationToken);
        var milestones = await dashboardRepository.GetMilestoneTotalsAsync(projectId, now, cancellationToken);
        var revisions = await dashboardRepository.CountProjectRevisionSubmissionsAsync(projectId, cancellationToken);
        var missed = await dashboardRepository.CountMissedMeetingsAsync(projectId, now, cancellationToken);
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
        var snapshot = await dashboardRepository.FindRiskSnapshotAsync(projectId, cancellationToken);
        if (snapshot is null)
        {
            snapshot = new ProjectRiskSnapshot(projectId, assessment.Score, assessment.Level, reasonsJson, now);
            dashboardRepository.AddRiskSnapshot(snapshot);
        }
        else snapshot.Apply(assessment.Score, assessment.Level, reasonsJson, now);
        dashboardRepository.AddRiskHistory(new ProjectRiskHistory(projectId, assessment.Score, assessment.Level, reasonsJson, now));
        dashboardRepository.AddProjectActivity(new ProjectActivity(projectId, "PROJECT_RISK_REFRESHED", null, reasonsJson));
        dashboardRepository.AddOutboxMessage(new OutboxMessage("project.risk.refreshed", JsonSerializer.Serialize(new { projectId, assessment.Score, assessment.Level, calculatedAt = now })));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ProjectRiskResponse(assessment.Score, assessment.Level, assessment.Reasons, now);
    }
}
