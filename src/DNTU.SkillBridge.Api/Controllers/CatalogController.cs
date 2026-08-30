using Asp.Versioning;
using DNTU.SkillBridge.Api.Catalog;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>Read-only catalog endpoints powering every frontend dropdown and metadata panel.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/catalog")]
[Produces("application/json")]
public sealed class CatalogController(CatalogService catalogService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Lists active skills (all skills when an administrator requests inactive entries).</summary>
    [HttpGet("skills")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<SkillResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SkillResponse>>>> GetSkills(
        [FromQuery] CatalogListQuery query, CancellationToken cancellationToken) =>
        await RespondAsync(query.IncludeInactive, includeInactive => catalogService.GetSkillsAsync(includeInactive, cancellationToken));

    /// <summary>Lists active industries used to classify companies and projects.</summary>
    [HttpGet("industries")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<IndustryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<IndustryResponse>>>> GetIndustries(
        [FromQuery] CatalogListQuery query, CancellationToken cancellationToken) =>
        await RespondAsync(query.IncludeInactive, includeInactive => catalogService.GetIndustriesAsync(includeInactive, cancellationToken));

    /// <summary>Lists active faculties; majors are grouped beneath each faculty.</summary>
    [HttpGet("faculties")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<FacultyResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<FacultyResponse>>>> GetFaculties(
        [FromQuery] CatalogListQuery query, CancellationToken cancellationToken) =>
        await RespondAsync(query.IncludeInactive, includeInactive => catalogService.GetFacultiesAsync(includeInactive, cancellationToken));

    /// <summary>Lists active majors, optionally filtered to a single faculty.</summary>
    [HttpGet("majors")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<MajorResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MajorResponse>>>> GetMajors(
        [FromQuery] MajorListQuery query, CancellationToken cancellationToken) =>
        await RespondAsync(query.IncludeInactive, includeInactive => catalogService.GetMajorsAsync(query.FacultyId, includeInactive, cancellationToken));

    /// <summary>Lists supported banks with their BIN codes for allowance and payment forms.</summary>
    [HttpGet("banks")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<BankResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<BankResponse>>>> GetBanks(
        [FromQuery] CatalogListQuery query, CancellationToken cancellationToken) =>
        await RespondAsync(query.IncludeInactive, includeInactive => catalogService.GetBanksAsync(includeInactive, cancellationToken));

    /// <summary>Returns the configured project metadata: difficulties, work types, duration, team size, and allowance bounds.</summary>
    [HttpGet("project-metadata")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ProjectMetadataResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProjectMetadataResponse>>> GetProjectMetadata(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<ProjectMetadataResponse>(await catalogService.GetProjectMetadataAsync(cancellationToken)));

    /// <summary>Returns the configured task metadata: kanban statuses and priorities.</summary>
    [HttpGet("task-metadata")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TaskMetadataResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TaskMetadataResponse>>> GetTaskMetadata(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<TaskMetadataResponse>(await catalogService.GetTaskMetadataAsync(cancellationToken)));

    private async Task<ActionResult> RespondAsync<TItem>(
        bool includeInactiveRequested,
        Func<bool, Task<IReadOnlyCollection<TItem>>> load)
    {
        var includeInactive = includeInactiveRequested && CatalogService.CanIncludeInactive(currentUser);
        if (includeInactiveRequested && !includeInactive)
        {
            return currentUser.IsAuthenticated ? Forbid() : Unauthorized();
        }

        return Ok(new ApiResponse<IReadOnlyCollection<TItem>>(await load(includeInactive)));
    }
}
