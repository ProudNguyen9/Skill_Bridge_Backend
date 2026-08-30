using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Api.Students;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>
/// Authenticated student saved-project endpoints: idempotent save/remove of publicly
/// visible projects, the paged saved list, and the informational skill match.
/// The student identity is always taken from the session, never from the payload.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/students/me")]
[Authorize]
[Produces("application/json")]
public sealed class SavedProjectsController(SavedProjectService savedProjectService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Saves a publicly visible project. Idempotent; answers with the current saved list. Drafts and hidden projects answer 404.</summary>
    [HttpPost("saved-projects/{projectId:guid}")]
    [ProducesResponseType(typeof(PagedResponse<SavedProjectListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<SavedProjectListItemResponse>>> Save(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        var outcome = await savedProjectService.SaveProjectAsync(currentUser.UserId!.Value, projectId, cancellationToken);
        if (outcome is SavedProjectOutcome.NotFound or SavedProjectOutcome.ProjectNotVisible)
        {
            return NotFound();
        }

        return Ok(await savedProjectService.ListSavedProjectsAsync(
            currentUser.UserId!.Value, new PageQuery(), cancellationToken));
    }

    /// <summary>Removes the caller's own bookmark; other students' saved rows are not visible. Unknown bookmarks answer 404.</summary>
    [HttpDelete("saved-projects/{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unsave(Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        return await savedProjectService.UnsaveProjectAsync(currentUser.UserId!.Value, projectId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>Lists the caller's saved projects, newest first. Projects that are no longer publicly visible are hidden from the list.</summary>
    [HttpGet("saved-projects")]
    [ProducesResponseType(typeof(PagedResponse<SavedProjectListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<SavedProjectListItemResponse>>> List(
        [FromQuery] PageQuery query, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        if (query.Page < 1 || query.PageSize is < 1 or > PageQuery.MaximumPageSize)
        {
            ModelState.AddModelError(nameof(query.PageSize), $"Page must be at least 1 and pageSize must be between 1 and {PageQuery.MaximumPageSize}.");
            return ValidationProblem(ModelState);
        }

        return Ok(await savedProjectService.ListSavedProjectsAsync(currentUser.UserId!.Value, query, cancellationToken));
    }

    /// <summary>
    /// Compares the caller's active declared skills with a publicly visible project's skill
    /// requirements. Informational only; never an application eligibility decision.
    /// Invisible projects answer 404.
    /// </summary>
    [HttpGet("skill-match/{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SkillMatchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SkillMatchResponse>>> SkillMatch(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        var match = await savedProjectService.MatchSkillsWithProjectAsync(currentUser.UserId!.Value, projectId, cancellationToken);
        return match is null
            ? NotFound()
            : Ok(new ApiResponse<SkillMatchResponse>(match));
    }

    private bool IsStudent() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Student);
}
