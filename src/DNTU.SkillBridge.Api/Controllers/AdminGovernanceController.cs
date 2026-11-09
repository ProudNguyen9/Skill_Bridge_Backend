using Asp.Versioning;
using DNTU.SkillBridge.Application.Administration;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/admin")]
[Authorize]
[Produces("application/json")]
public sealed class AdminGovernanceController(IAdminGovernanceService governanceService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("users")]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.SuperAdmin)]
    [ProducesResponseType(typeof(PagedResponse<AdminUserListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<AdminUserListItemResponse>>> ListUsers(
        [FromQuery] AdminUserListQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1)
            ModelState.AddModelError(nameof(query.Page), "Page must be at least 1.");
        if (query.PageSize is < 1 or > PageQuery.MaximumPageSize)
            ModelState.AddModelError(nameof(query.PageSize), $"PageSize must be between 1 and {PageQuery.MaximumPageSize}.");
        if (Request.Query[nameof(query.Search)].ToString().Length > AdminUserListQuery.MaximumSearchLength)
            ModelState.AddModelError(nameof(query.Search), $"Search must not exceed {AdminUserListQuery.MaximumSearchLength} characters.");
        if (Request.Query.ContainsKey(nameof(query.Role)) && query.Role?.Trim().ToUpperInvariant() is not
            (RoleNames.Student or RoleNames.Company or RoleNames.Lecturer or RoleNames.Admin or RoleNames.SuperAdmin))
            ModelState.AddModelError(nameof(query.Role), "Role must be an existing known role name.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        return Ok(await governanceService.ListUsersAsync(query, cancellationToken));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<AdminDashboardResponse>>> Dashboard(CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        return Ok(new ApiResponse<AdminDashboardResponse>(await governanceService.GetDashboardAsync(cancellationToken)));
    }

    [HttpGet("settings/{category}")]
    public async Task<ActionResult<ApiResponse<AdminPolicyResponse>>> GetPolicy(string category, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var policy = await governanceService.GetPolicyAsync(category, cancellationToken);
        return policy is null ? NotFound() : Ok(new ApiResponse<AdminPolicyResponse>(policy));
    }

    [HttpPut("settings/{category}")]
    public async Task<ActionResult<ApiResponse<AdminPolicyResponse>>> UpdatePolicy(string category, [FromBody] JsonSettingsRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var policy = await governanceService.UpdatePolicyAsync(currentUser.UserId!.Value, category, request.SettingsJson, cancellationToken);
        return Ok(new ApiResponse<AdminPolicyResponse>(policy));
    }

    private bool IsAdmin() => currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin);
}

public sealed record JsonSettingsRequest(string SettingsJson);
