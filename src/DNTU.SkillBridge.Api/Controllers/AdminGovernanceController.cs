using Asp.Versioning;
using DNTU.SkillBridge.Api.Administration;
using DNTU.SkillBridge.Api.Contracts;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize]
[Produces("application/json")]
public sealed class AdminGovernanceController(AdminGovernanceService governanceService, ICurrentUser currentUser) : ControllerBase
{
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
