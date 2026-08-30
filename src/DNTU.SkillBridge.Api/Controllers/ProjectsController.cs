using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>
/// Anonymous public project browsing: search, filter, sort, detail by slug,
/// related projects, skill requirements, and public milestones.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class ProjectsController(IProjectService projectService) : ControllerBase
{
    /// <summary>
    /// Browses publicly visible projects with optional search (title/summary), skill,
    /// industry, difficulty, workType, duration and allowance range filters, and a
    /// named sort allow-list (newest, oldest, deadline, title, allowance).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PublicProjectListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<PublicProjectListItemResponse>>> List(
        [FromQuery] PublicProjectFilterQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > PageQuery.MaximumPageSize)
        {
            ModelState.AddModelError(nameof(query.PageSize), $"Page must be at least 1 and pageSize must be between 1 and {PageQuery.MaximumPageSize}.");
            return ValidationProblem(ModelState);
        }

        if (!ProjectService.IsPublicSortAllowed(query.Sort))
        {
            ModelState.AddModelError(nameof(query.Sort), "Sort must be one of: newest, oldest, deadline, title, allowance.");
            return ValidationProblem(ModelState);
        }

        return Ok(await projectService.SearchPublicProjectsAsync(query, cancellationToken));
    }

    /// <summary>Returns one publicly visible project by slug; drafts and hidden projects answer 404.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ApiResponse<PublicProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PublicProjectDetailResponse>>> GetBySlug(
        string slug, CancellationToken cancellationToken)
    {
        var project = await projectService.GetPublicProjectBySlugAsync(slug, cancellationToken);
        return project is null
            ? NotFound()
            : Ok(new ApiResponse<PublicProjectDetailResponse>(project));
    }

    /// <summary>Lists publicly visible projects sharing at least one skill with the given project, most-shared first.</summary>
    [HttpGet("{projectId:guid}/related")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<PublicProjectListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PublicProjectListItemResponse>>>> Related(
        Guid projectId, [FromQuery] int take = 5, CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 20)
        {
            ModelState.AddModelError(nameof(take), "take must be between 1 and 20.");
            return ValidationProblem(ModelState);
        }

        var related = await projectService.ListRelatedProjectsAsync(projectId, take, cancellationToken);
        return related is null
            ? NotFound()
            : Ok(new ApiResponse<IReadOnlyCollection<PublicProjectListItemResponse>>(related));
    }

    /// <summary>Lists the skill requirements of a publicly visible project.</summary>
    [HttpGet("{projectId:guid}/skills")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<PublicProjectSkillResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PublicProjectSkillResponse>>>> Skills(
        Guid projectId, CancellationToken cancellationToken)
    {
        var skills = await projectService.ListPublicProjectSkillsAsync(projectId, cancellationToken);
        return skills is null
            ? NotFound()
            : Ok(new ApiResponse<IReadOnlyCollection<PublicProjectSkillResponse>>(skills));
    }

    /// <summary>
    /// Public milestones; empty until the milestone module (Task 24) delivers milestone data.
    /// Inaccessible projects answer 404.
    /// </summary>
    [HttpGet("{projectId:guid}/public-milestones")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> PublicMilestones(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!await projectService.IsPubliclyAccessibleAsync(projectId, cancellationToken))
        {
            return NotFound();
        }

        var milestones = await projectService.ListPublicProjectMilestonesAsync(projectId, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyCollection<object>>(milestones));
    }
}
