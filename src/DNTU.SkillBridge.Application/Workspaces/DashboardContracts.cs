using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

public sealed record ProjectProgressResponse(
    Guid ProjectId,
    int TotalTasks,
    int CompletedTasks,
    int OverdueTasks,
    int TotalMilestones,
    int ApprovedMilestones,
    int OverdueMilestones,
    decimal CompletionPercent,
    ProjectRiskResponse Risk);

public sealed record ProjectRiskResponse(
    int Score,
    ProjectRiskLevel Level,
    IReadOnlyCollection<string> Reasons,
    DateTimeOffset CalculatedAt);

public sealed record DashboardProjectResponse(
    Guid ProjectId,
    string Title,
    string Status,
    ProjectProgressResponse Progress);

public sealed record StudentDashboardResponse(
    IReadOnlyCollection<DashboardProjectResponse> CurrentProjects,
    int AssignedTasks,
    int UpcomingMeetings,
    int RevisionSubmissions,
    int UnreadNotifications);

public sealed record CompanyDashboardResponse(
    int ActiveProjects,
    int PendingApproval,
    int RecruitingProjects,
    int ApplicationsWaiting,
    int SubmissionsWaiting,
    int UpcomingMeetings,
    IReadOnlyCollection<DashboardProjectResponse> Projects);

public sealed record LecturerDashboardResponse(
    IReadOnlyCollection<DashboardProjectResponse> ProjectsAtRisk,
    int OverdueMilestones,
    int SubmissionsWaiting,
    int UpcomingMeetings,
    int PendingWithdrawal,
    int RecentActivity);
