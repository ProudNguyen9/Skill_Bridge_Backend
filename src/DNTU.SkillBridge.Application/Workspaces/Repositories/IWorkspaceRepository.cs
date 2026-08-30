using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

/// <summary>Projected project fields used to build the workspace overview payload.</summary>
public sealed record WorkspaceProjectRow(Guid Id, string Title, string Slug, ProjectStatus Status);

/// <summary>Correlated project summary with member and task counts used by the student's project list.</summary>
public sealed record WorkspaceProjectSummaryRow(
    Guid Id,
    string Title,
    string Slug,
    ProjectStatus Status,
    int MemberCount,
    int OpenTaskCount,
    int OverdueTaskCount);

/// <summary>Read and write data operations for the workspace feature.</summary>
public interface IWorkspaceRepository
{
    Task<WorkspaceProjectRow?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<int> CountActiveMembersAsync(Guid projectId, CancellationToken cancellationToken);

    Task<int> CountOpenTasksAsync(Guid projectId, CancellationToken cancellationToken);

    Task<int> CountOverdueTasksAsync(Guid projectId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WorkspaceMemberResponse>> ListTeamAsync(Guid projectId, CancellationToken cancellationToken);

    Task<(int TotalCount, IReadOnlyCollection<WorkspaceActivityResponse> Items)> ListActivityAsync(Guid projectId, string? eventType, WorkspaceActivityQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WorkspaceProjectSummaryRow>> ListMyProjectsAsync(Guid userId, CancellationToken cancellationToken);

    Task<WorkspaceSettings?> FindSettingsAsync(Guid projectId, CancellationToken cancellationToken);

    Task<WorkspaceSettings?> FindSettingsForUpdateAsync(Guid projectId, CancellationToken cancellationToken);

    void AddSettings(WorkspaceSettings settings);
}
