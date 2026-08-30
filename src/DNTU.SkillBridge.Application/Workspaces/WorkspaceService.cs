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

public sealed class WorkspaceService(IWorkspaceRepository workspaceRepository, IProjectAccessService projectAccessService, IUnitOfWork unitOfWork) : IWorkspaceService
{
    public async Task<WorkspaceOverviewResponse?> GetOverviewAsync(ICurrentUser currentUser, Guid projectId, CancellationToken cancellationToken)
    {
        var access = await projectAccessService.GetAsync(currentUser, projectId, cancellationToken);
        if (!access.CanRead) return null;

        var project = await workspaceRepository.FindProjectAsync(projectId, cancellationToken);
        if (project is null) return null;

        var memberCount = await workspaceRepository.CountActiveMembersAsync(projectId, cancellationToken);
        var openTaskCount = await workspaceRepository.CountOpenTasksAsync(projectId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var overdueTaskCount = await workspaceRepository.CountOverdueTasksAsync(projectId, now, cancellationToken);
        return new WorkspaceOverviewResponse(project.Id, project.Title, project.Slug, project.Status.ToString(), memberCount, openTaskCount, overdueTaskCount, access.Capabilities);
    }

    public async Task<IReadOnlyCollection<WorkspaceMemberResponse>?> GetTeamAsync(ICurrentUser currentUser, Guid projectId, CancellationToken cancellationToken)
    {
        if (!(await projectAccessService.GetAsync(currentUser, projectId, cancellationToken)).CanRead) return null;
        return await workspaceRepository.ListTeamAsync(projectId, cancellationToken);
    }

    public async Task<PagedResponse<WorkspaceActivityResponse>?> GetActivityAsync(ICurrentUser currentUser, Guid projectId, WorkspaceActivityQuery query, CancellationToken cancellationToken)
    {
        if (!(await projectAccessService.GetAsync(currentUser, projectId, cancellationToken)).CanRead) return null;
        string? eventType = null;
        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            eventType = query.EventType.Trim().ToUpperInvariant();
        }

        var (total, items) = await workspaceRepository.ListActivityAsync(projectId, eventType, query, cancellationToken);
        return new PagedResponse<WorkspaceActivityResponse>(items, PageMetadata.Create(query.Page, query.PageSize, total));
    }

    public async Task<IReadOnlyCollection<WorkspaceOverviewResponse>> ListMyProjectsAsync(ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId) return [];

        var rows = await workspaceRepository.ListMyProjectsAsync(userId, cancellationToken);
        return rows.Select(project => new WorkspaceOverviewResponse(
            project.Id,
            project.Title,
            project.Slug,
            project.Status.ToString(),
            project.MemberCount,
            project.OpenTaskCount,
            project.OverdueTaskCount,
            new HashSet<string>(["workspace.read"])))
            .ToList();
    }

    public async Task<WorkspaceSettingsResponse?> GetSettingsAsync(ICurrentUser currentUser, Guid projectId, CancellationToken cancellationToken)
    {
        if (!(await projectAccessService.GetAsync(currentUser, projectId, cancellationToken)).CanRead) return null;
        var settings = await workspaceRepository.FindSettingsAsync(projectId, cancellationToken);
        return settings is null ? new WorkspaceSettingsResponse(projectId, true, true, null, null) : MapSettings(settings);
    }

    public async Task<WorkspaceSettingsResponse?> UpdateSettingsAsync(ICurrentUser currentUser, Guid projectId, UpdateWorkspaceSettingsRequest request, CancellationToken cancellationToken)
    {
        var access = await projectAccessService.GetAsync(currentUser, projectId, cancellationToken);
        if (!access.CanManage) return null;
        var settings = await workspaceRepository.FindSettingsForUpdateAsync(projectId, cancellationToken);
        if (settings is null)
        {
            settings = new WorkspaceSettings(projectId);
            workspaceRepository.AddSettings(settings);
        }
        settings.Update(request.MembersCanCreateTasks, request.MembersCanScheduleMeetings, request.WorkingAgreement);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MapSettings(settings);
    }

    private static WorkspaceSettingsResponse MapSettings(WorkspaceSettings settings) => new(settings.ProjectId, settings.MembersCanCreateTasks, settings.MembersCanScheduleMeetings, settings.WorkingAgreement, settings.UpdatedAt);
}
