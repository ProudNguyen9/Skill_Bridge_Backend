using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Api.Workspaces;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
[Produces("application/json")]
public sealed class DashboardsController(DashboardService dashboardService, ICurrentUser currentUser) : ControllerBase
{
    // Dynamic server-derived progress/risk data is deliberately non-cacheable until a tagged
    // invalidation strategy exists; payment state is never part of these read models.
    private void PreventCaching() => Response.Headers.CacheControl = "no-store, no-cache, max-age=0";

    [HttpGet("student/dashboard")]
    [ProducesResponseType(typeof(ApiResponse<StudentDashboardResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StudentDashboardResponse>>> Student(CancellationToken cancellationToken)
    {
        if (!HasRole(RoleNames.Student)) return Forbid();
        PreventCaching();
        var dashboard = await dashboardService.GetStudentDashboardAsync(currentUser.UserId!.Value, cancellationToken);
        return dashboard is null ? NotFound() : Ok(new ApiResponse<StudentDashboardResponse>(dashboard));
    }

    [HttpGet("company/dashboard")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDashboardResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CompanyDashboardResponse>>> Company(CancellationToken cancellationToken)
    {
        if (!HasRole(RoleNames.Company)) return Forbid();
        PreventCaching();
        var dashboard = await dashboardService.GetCompanyDashboardAsync(currentUser.UserId!.Value, cancellationToken);
        return dashboard is null ? NotFound() : Ok(new ApiResponse<CompanyDashboardResponse>(dashboard));
    }

    [HttpGet("lecturer/dashboard")]
    [ProducesResponseType(typeof(ApiResponse<LecturerDashboardResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LecturerDashboardResponse>>> Lecturer(CancellationToken cancellationToken)
    {
        if (!HasRole(RoleNames.Lecturer)) return Forbid();
        PreventCaching();
        var dashboard = await dashboardService.GetLecturerDashboardAsync(currentUser.UserId!.Value, cancellationToken);
        return dashboard is null ? NotFound() : Ok(new ApiResponse<LecturerDashboardResponse>(dashboard));
    }

    [HttpGet("projects/{projectId:guid}/progress")]
    [ProducesResponseType(typeof(ApiResponse<ProjectProgressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProjectProgressResponse>>> Progress(Guid projectId, CancellationToken cancellationToken)
    {
        PreventCaching();
        var progress = await dashboardService.GetProjectProgressAsync(currentUser.UserId!.Value, projectId, cancellationToken);
        return progress is null ? NotFound() : Ok(new ApiResponse<ProjectProgressResponse>(progress));
    }

    private bool HasRole(string role) => currentUser.IsAuthenticated && currentUser.Roles.Contains(role);
}
