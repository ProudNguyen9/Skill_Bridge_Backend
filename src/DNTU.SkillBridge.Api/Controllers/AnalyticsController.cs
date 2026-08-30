using Asp.Versioning;
using DNTU.SkillBridge.Api.Analytics;
using DNTU.SkillBridge.Api.Contracts;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/analytics")]
[Authorize]
[Produces("application/json")]
public sealed class AnalyticsController(AnalyticsService analyticsService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<AnalyticsOverviewResponse>>> Overview(CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        return Ok(new ApiResponse<AnalyticsOverviewResponse>(await analyticsService.GetOverviewAsync(cancellationToken)));
    }

    [HttpGet("skills/gaps")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SkillAnalyticsResponse>>>> SkillGaps(CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<SkillAnalyticsResponse>>(await analyticsService.GetSkillsAsync(cancellationToken)));
    }

    [HttpGet("skills/demand")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<SkillAnalyticsResponse>>>> SkillDemand(CancellationToken cancellationToken) => SkillGaps(cancellationToken);

    [HttpGet("skills/supply")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<SkillAnalyticsResponse>>>> SkillSupply(CancellationToken cancellationToken) => SkillGaps(cancellationToken);

    private bool IsAdmin() => currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin);
}
