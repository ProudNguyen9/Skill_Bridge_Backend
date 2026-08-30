using Asp.Versioning;
using DNTU.SkillBridge.Api.Catalog;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>Standalone skill listing alias of the catalog skills endpoint.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/skills")]
[Produces("application/json")]
public sealed class SkillsController(CatalogService catalogService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Lists active skills (all skills when an administrator requests inactive entries).</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<SkillResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SkillResponse>>>> GetSkills(
        [FromQuery] CatalogListQuery query, CancellationToken cancellationToken)
    {
        var includeInactive = query.IncludeInactive && CatalogService.CanIncludeInactive(currentUser);
        if (query.IncludeInactive && !includeInactive)
        {
            return currentUser.IsAuthenticated ? Forbid() : Unauthorized();
        }

        return Ok(new ApiResponse<IReadOnlyCollection<SkillResponse>>(
            await catalogService.GetSkillsAsync(includeInactive, cancellationToken)));
    }
}
