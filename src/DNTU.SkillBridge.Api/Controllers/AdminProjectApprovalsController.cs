using Asp.Versioning;
using DNTU.SkillBridge.Api.Contracts;
using DNTU.SkillBridge.Api.Projects;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>
/// Admin approval workflow for company projects (Task 13). Actions require the
/// projects.approve permission or an ADMIN/SUPER_ADMIN role; the dual check keeps
/// both permission-based and role-based principals working.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize]
[Produces("application/json")]
public sealed class AdminProjectApprovalsController(ProjectService projectService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Lists the admin approval queue (all non-draft projects), newest submissions first.</summary>
    [HttpGet("project-approvals")]
    [ProducesResponseType(typeof(PagedResponse<AdminApprovalListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<AdminApprovalListItemResponse>>> List(
        [FromQuery] PageQuery query, string? status, CancellationToken cancellationToken)
    {
        if (!CanApprove())
        {
            return Forbid();
        }

        if (query.Page < 1 || query.PageSize is < 1 or > PageQuery.MaximumPageSize)
        {
            ModelState.AddModelError(nameof(query.PageSize), $"Page must be at least 1 and pageSize must be between 1 and {PageQuery.MaximumPageSize}.");
            return ValidationProblem(ModelState);
        }

        return Ok(await projectService.ListApprovalsAsync(status, query, cancellationToken));
    }

    /// <summary>Returns one project with its full append-only decision history.</summary>
    [HttpGet("project-approvals/{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminApprovalDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminApprovalDetailResponse>>> Get(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!CanApprove())
        {
            return Forbid();
        }

        var detail = await projectService.GetApprovalDetailAsync(projectId, cancellationToken);
        return detail is null ? NotFound() : Ok(new ApiResponse<AdminApprovalDetailResponse>(detail));
    }

    /// <summary>Approves a pending-approval project; approval publishes it (status-driven visibility).</summary>
    [HttpPost("projects/{projectId:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<AdminApprovalDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AdminApprovalDetailResponse>>> Approve(
        Guid projectId, [FromBody] ApprovalDecisionRequest? request, CancellationToken cancellationToken) =>
        await Decide(projectId, ProjectDecision.APPROVED, request?.Note, cancellationToken);

    /// <summary>Requests changes on a pending-approval project, sending it back to the company.</summary>
    [HttpPost("projects/{projectId:guid}/request-changes")]
    [ProducesResponseType(typeof(ApiResponse<AdminApprovalDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AdminApprovalDetailResponse>>> RequestChanges(
        Guid projectId, [FromBody] ApprovalDecisionRequest? request, CancellationToken cancellationToken) =>
        await Decide(projectId, ProjectDecision.CHANGES_REQUESTED, request?.Note, cancellationToken);

    /// <summary>Rejects a pending-approval project with an optional reason.</summary>
    [HttpPost("projects/{projectId:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<AdminApprovalDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AdminApprovalDetailResponse>>> Reject(
        Guid projectId, [FromBody] ApprovalDecisionRequest? request, CancellationToken cancellationToken) =>
        await Decide(projectId, ProjectDecision.REJECTED, request?.Note, cancellationToken);

    /// <summary>Suspends an approved project, withdrawing it from public visibility.</summary>
    [HttpPost("projects/{projectId:guid}/suspend")]
    [ProducesResponseType(typeof(ApiResponse<AdminApprovalDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AdminApprovalDetailResponse>>> Suspend(
        Guid projectId, [FromBody] ApprovalDecisionRequest? request, CancellationToken cancellationToken) =>
        await Decide(projectId, ProjectDecision.SUSPENDED, request?.Note, cancellationToken);

    /// <summary>Resumes a suspended project back to APPROVED (publicly visible again).</summary>
    [HttpPost("projects/{projectId:guid}/resume")]
    [ProducesResponseType(typeof(ApiResponse<AdminApprovalDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AdminApprovalDetailResponse>>> Resume(
        Guid projectId, [FromBody] ApprovalDecisionRequest? request, CancellationToken cancellationToken) =>
        await Decide(projectId, ProjectDecision.RESUMED, request?.Note, cancellationToken);

    private async Task<ActionResult<ApiResponse<AdminApprovalDetailResponse>>> Decide(
        Guid projectId, ProjectDecision decision, string? note, CancellationToken cancellationToken)
    {
        if (!CanApprove())
        {
            return Forbid();
        }

        var (outcome, detail) = await projectService.DecideAsync(
            projectId, currentUser.UserId!.Value, decision, note, cancellationToken);
        return outcome switch
        {
            AdminDecisionOutcome.NotFound => NotFound(),
            AdminDecisionOutcome.InvalidTransition => Conflict(),
            _ => Ok(new ApiResponse<AdminApprovalDetailResponse>(detail!))
        };
    }

    /// <summary>Permission-based or role-based authorization so crafted role JWTs (tests) and real permission grants both work.</summary>
    private bool CanApprove() =>
        currentUser.IsAuthenticated &&
        (currentUser.Permissions.Contains(PermissionNames.ProjectsApprove) ||
         currentUser.Roles.Contains(RoleNames.Admin) ||
         currentUser.Roles.Contains(RoleNames.SuperAdmin));
}
