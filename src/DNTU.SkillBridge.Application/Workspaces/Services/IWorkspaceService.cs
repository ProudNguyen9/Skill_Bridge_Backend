using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

public interface IWorkspaceService
{
    Task<WorkspaceOverviewResponse?> GetOverviewAsync(ICurrentUser currentUser, Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WorkspaceMemberResponse>?> GetTeamAsync(ICurrentUser currentUser, Guid projectId, CancellationToken cancellationToken);

    Task<PagedResponse<WorkspaceActivityResponse>?> GetActivityAsync(ICurrentUser currentUser, Guid projectId, WorkspaceActivityQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WorkspaceOverviewResponse>> ListMyProjectsAsync(ICurrentUser currentUser, CancellationToken cancellationToken);

    Task<WorkspaceSettingsResponse?> GetSettingsAsync(ICurrentUser currentUser, Guid projectId, CancellationToken cancellationToken);

    Task<WorkspaceSettingsResponse?> UpdateSettingsAsync(ICurrentUser currentUser, Guid projectId, UpdateWorkspaceSettingsRequest request, CancellationToken cancellationToken);
}
