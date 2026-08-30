using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
[Produces("application/json")]
public sealed class WorkspacesController(IWorkspaceService workspaceService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("workspaces/{projectId:guid}/overview")]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceOverviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<WorkspaceOverviewResponse>>> Overview(Guid projectId, CancellationToken cancellationToken)
    {
        var overview = await workspaceService.GetOverviewAsync(currentUser, projectId, cancellationToken);
        return overview is null ? NotFound() : Ok(new ApiResponse<WorkspaceOverviewResponse>(overview));
    }

    [HttpGet("workspaces/{projectId:guid}/team")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<WorkspaceMemberResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<WorkspaceMemberResponse>>>> Team(Guid projectId, CancellationToken cancellationToken)
    {
        var team = await workspaceService.GetTeamAsync(currentUser, projectId, cancellationToken);
        return team is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<WorkspaceMemberResponse>>(team));
    }

    [HttpGet("workspaces/{projectId:guid}/activity")]
    [ProducesResponseType(typeof(PagedResponse<WorkspaceActivityResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<WorkspaceActivityResponse>>> Activity(Guid projectId, [FromQuery] WorkspaceActivityQuery query, CancellationToken cancellationToken)
    {
        var activity = await workspaceService.GetActivityAsync(currentUser, projectId, query, cancellationToken);
        return activity is null ? NotFound() : Ok(activity);
    }

    [HttpGet("workspaces/{projectId:guid}/settings")]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceSettingsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WorkspaceSettingsResponse>>> GetSettings(Guid projectId, CancellationToken cancellationToken)
    {
        var settings = await workspaceService.GetSettingsAsync(currentUser, projectId, cancellationToken);
        return settings is null ? NotFound() : Ok(new ApiResponse<WorkspaceSettingsResponse>(settings));
    }

    [HttpPut("workspaces/{projectId:guid}/settings")]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceSettingsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<WorkspaceSettingsResponse>>> UpdateSettings(Guid projectId, [FromBody] UpdateWorkspaceSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = await workspaceService.UpdateSettingsAsync(currentUser, projectId, request, cancellationToken);
        return settings is null ? NotFound() : Ok(new ApiResponse<WorkspaceSettingsResponse>(settings));
    }

    [HttpGet("projects/{projectId:guid}/activity")]
    public async Task<ActionResult<PagedResponse<WorkspaceActivityResponse>>> ProjectActivity(Guid projectId, [FromQuery] WorkspaceActivityQuery query, CancellationToken cancellationToken) =>
        await Activity(projectId, query, cancellationToken);

    [HttpGet("students/me/projects")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<WorkspaceOverviewResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<WorkspaceOverviewResponse>>>> MyProjects(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyCollection<WorkspaceOverviewResponse>>(await workspaceService.ListMyProjectsAsync(currentUser, cancellationToken)));

    [HttpGet("students/me/projects/{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceOverviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<WorkspaceOverviewResponse>>> MyProject(Guid projectId, CancellationToken cancellationToken) =>
        Overview(projectId, cancellationToken);
}
