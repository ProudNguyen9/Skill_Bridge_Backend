using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

/// <summary>Data operations for the student, company, and lecturer dashboards and project progress snapshots.</summary>
public interface IDashboardRepository
{
    Task<Guid?> FindStudentIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DashboardProjectRow>> ListStudentProjectRowsAsync(Guid studentId, CancellationToken cancellationToken);

    Task<int> CountAssignedTasksAsync(Guid studentId, CancellationToken cancellationToken);

    Task<int> CountUpcomingMeetingsAsync(Guid studentId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<int> CountRevisionSubmissionsAsync(Guid studentId, CancellationToken cancellationToken);

    Task<int> CountUnreadNotificationsAsync(Guid userId, CancellationToken cancellationToken);

    Task<Guid?> FindCompanyIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DashboardProjectRow>> ListCompanyProjectRowsAsync(Guid companyId, CancellationToken cancellationToken);

    Task<int> CountPendingApplicationsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    Task<int> CountWaitingSubmissionsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    Task<int> CountUpcomingProjectMeetingsAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken);

    Task<Guid?> FindLecturerIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DashboardProjectRow>> ListLecturerProjectRowsAsync(Guid lecturerId, CancellationToken cancellationToken);

    Task<int> CountOverdueMilestonesAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken);

    Task<int> CountRecentProjectActivityAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DashboardTaskGroupRow>> ListTaskGroupsAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DashboardMilestoneGroupRow>> ListMilestoneGroupsAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DashboardCountGroupRow>> ListRevisionGroupsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DashboardCountGroupRow>> ListMissedMeetingGroupsAsync(IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken);

    Task<DashboardTaskTotalsRow?> GetTaskTotalsAsync(Guid projectId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<DashboardMilestoneTotalsRow?> GetMilestoneTotalsAsync(Guid projectId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<int> CountProjectRevisionSubmissionsAsync(Guid projectId, CancellationToken cancellationToken);

    Task<int> CountMissedMeetingsAsync(Guid projectId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ProjectRiskSnapshot?> FindRiskSnapshotAsync(Guid projectId, CancellationToken cancellationToken);

    void AddRiskSnapshot(ProjectRiskSnapshot snapshot);

    void AddRiskHistory(ProjectRiskHistory history);

    void AddProjectActivity(ProjectActivity activity);

    void AddOutboxMessage(OutboxMessage message);

    Task<bool> CanReadAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);
}

/// <summary>Project row projected for dashboard overviews.</summary>
public sealed record DashboardProjectRow(Guid Id, string Title, ProjectStatus Status);

/// <summary>Grouped per-project task counts used by dashboard progress and risk calculations.</summary>
public sealed record DashboardTaskGroupRow(Guid ProjectId, int Total, int Done, int Overdue, int OldOverdue);

/// <summary>Grouped per-project milestone counts used by dashboard progress and risk calculations.</summary>
public sealed record DashboardMilestoneGroupRow(Guid ProjectId, int Total, int Approved, int Overdue);

/// <summary>Grouped per-project revision or missed-meeting counts.</summary>
public sealed record DashboardCountGroupRow(Guid ProjectId, int Count);

/// <summary>Aggregated task counts for a single project.</summary>
public sealed record DashboardTaskTotalsRow(int Total, int Done, int Overdue, int OldOverdue);

/// <summary>Aggregated milestone counts for a single project.</summary>
public sealed record DashboardMilestoneTotalsRow(int Total, int Approved, int Overdue);
