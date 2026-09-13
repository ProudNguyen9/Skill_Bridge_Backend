using Asp.Versioning;
using DNTU.SkillBridge.Application.Applications;
using DNTU.SkillBridge.Application.Companies;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Application.Projects;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>Company-scoped project draft endpoints (create, read, update, delete, team, progress).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/company/projects")]
[Authorize]
[Produces("application/json")]
public sealed class CompanyProjectsController(IProjectService projectService, IProjectWorkflowService workflowService, IProjectTeamService teamService, IApplicationService applicationService, ICompanyService companyService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Lists the caller's company projects with pagination and a validated sort allow-list.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CompanyProjectListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<CompanyProjectListItemResponse>>> List(
        [FromQuery] PageQuery query, string? sort, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        if (query.Page < 1 || query.PageSize is < 1 or > PageQuery.MaximumPageSize)
        {
            ModelState.AddModelError(nameof(query.PageSize), $"Page must be at least 1 and pageSize must be between 1 and {PageQuery.MaximumPageSize}.");
            return ValidationProblem(ModelState);
        }

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        if (!companyId.HasValue)
        {
            return NotFound();
        }

        return Ok(await projectService.ListCompanyProjectsAsync(companyId.Value, query, sort, cancellationToken));
    }

    /// <summary>Creates a new project draft for the caller's company.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CompanyProjectDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CompanyProjectDetailResponse>>> Create(
        CreateProjectRequest request, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        if (!companyId.HasValue)
        {
            return NotFound();
        }

        var (outcome, project) = await projectService.CreateProjectAsync(companyId.Value, request, cancellationToken);
        return outcome switch
        {
            ProjectCreateOutcome.CompanyNotFound => NotFound(),
            ProjectCreateOutcome.InvalidCatalog => InvalidCatalog(),
            ProjectCreateOutcome.InvalidTeamSize => InvalidTeamSize(),
            ProjectCreateOutcome.Conflict => Conflict(),
            _ => Created("", new ApiResponse<CompanyProjectDetailResponse>(project!))
        };
    }

    /// <summary>Returns one of the caller's company projects; other companies' projects answer 404.</summary>
    [HttpGet("{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompanyProjectDetailResponse>>> Get(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        if (!companyId.HasValue)
        {
            return NotFound();
        }

        var project = await projectService.GetCompanyProjectAsync(companyId.Value, projectId, cancellationToken);
        return project is null ? NotFound() : Ok(new ApiResponse<CompanyProjectDetailResponse>(project));
    }

    /// <summary>Updates a project draft; only DRAFT projects can be modified.</summary>
    [HttpPut("{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CompanyProjectDetailResponse>>> Update(
        Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        if (!companyId.HasValue)
        {
            return NotFound();
        }

        var (outcome, project) = await projectService.UpdateProjectAsync(companyId.Value, projectId, request, cancellationToken);
        return outcome switch
        {
            ProjectUpdateOutcome.NotFound => NotFound(),
            ProjectUpdateOutcome.NotDraft => Conflict(),
            ProjectUpdateOutcome.InvalidCatalog => InvalidCatalog(),
            ProjectUpdateOutcome.InvalidTeamSize => InvalidTeamSize(),
            ProjectUpdateOutcome.Conflict => Conflict(),
            _ => Ok(new ApiResponse<CompanyProjectDetailResponse>(project!))
        };
    }

    /// <summary>Soft-deletes a project draft; only DRAFT projects can be deleted.</summary>
    [HttpDelete("{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        if (!companyId.HasValue)
        {
            return NotFound();
        }

        var outcome = await projectService.DeleteProjectAsync(companyId.Value, projectId, cancellationToken);
        return outcome switch
        {
            ProjectDeleteOutcome.Deleted => NoContent(),
            ProjectDeleteOutcome.NotDraft => Conflict(),
            _ => NotFound()
        };
    }

    /// <summary>Team composition preview; empty until the team module (Task 17) arrives.</summary>
    [HttpGet("{projectId:guid}/team")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> GetTeam(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        if (!companyId.HasValue)
        {
            return NotFound();
        }

        var team = await teamService.GetProjectTeamAsync(companyId.Value, projectId, cancellationToken);
        return team is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<object>>(team));
    }

    /// <summary>Lists applications for one project owned by the caller's company.</summary>
    [HttpGet("{projectId:guid}/applications")]
    [ProducesResponseType(typeof(PagedResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ApplicationResponse>>> ListApplications(
        Guid projectId, [FromQuery] CompanyApplicationListQuery query, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        if (!companyId.HasValue || await projectService.GetCompanyProjectAsync(companyId.Value, projectId, cancellationToken) is null)
        {
            return NotFound();
        }

        query = new CompanyApplicationListQuery
        {
            ProjectId = projectId,
            Status = query.Status,
            Page = query.Page,
            PageSize = query.PageSize
        };
        return Ok(await applicationService.ListCompanyApplicationsAsync(companyId.Value, query, cancellationToken));
    }

    /// <summary>Safe draft-compatible progress projection for the project.</summary>
    [HttpGet("{projectId:guid}/progress")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProjectProgressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompanyProjectProgressResponse>>> GetProgress(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        if (!companyId.HasValue)
        {
            return NotFound();
        }

        var progress = await teamService.GetProjectProgressAsync(companyId.Value, projectId, cancellationToken);
        return progress is null ? NotFound() : Ok(new ApiResponse<CompanyProjectProgressResponse>(progress));
    }

    /// <summary>Submits a draft (or changes-requested project) for admin review after readiness checks.</summary>
    [HttpPost("{projectId:guid}/submit")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CompanyProjectDetailResponse>>> Submit(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        if (!companyId.HasValue)
        {
            return NotFound();
        }

        var (outcome, project) = await workflowService.SubmitForApprovalAsync(
            companyId.Value, projectId, currentUser.UserId!.Value, cancellationToken);
        return outcome switch
        {
            ProjectSubmitOutcome.NotFound => NotFound(),
            ProjectSubmitOutcome.CompanyNotVerified => Conflict(),
            ProjectSubmitOutcome.Incomplete => Conflict(),
            ProjectSubmitOutcome.InvalidDeadline => Conflict(),
            ProjectSubmitOutcome.NotSubmittable => Conflict(),
            _ => Ok(new ApiResponse<CompanyProjectDetailResponse>(project!))
        };
    }

    /// <summary>Cancels a draft or pending-approval project; already-published projects cannot be cancelled here.</summary>
    [HttpPost("{projectId:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CompanyProjectDetailResponse>>> Cancel(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        if (!companyId.HasValue)
        {
            return NotFound();
        }

        var (outcome, project) = await workflowService.CancelAsync(
            companyId.Value, projectId, currentUser.UserId!.Value, cancellationToken);
        return outcome switch
        {
            ProjectCancelOutcome.NotFound => NotFound(),
            ProjectCancelOutcome.InvalidTransition => Conflict(),
            _ => Ok(new ApiResponse<CompanyProjectDetailResponse>(project!))
        };
    }

    /// <summary>Reopens a rejected, changes-requested, or cancelled project back into an editable draft.</summary>
    [HttpPost("{projectId:guid}/reopen")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CompanyProjectDetailResponse>>> Reopen(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        if (!companyId.HasValue)
        {
            return NotFound();
        }

        var (outcome, project) = await workflowService.ReopenAsync(
            companyId.Value, projectId, currentUser.UserId!.Value, cancellationToken);
        return outcome switch
        {
            ProjectReopenOutcome.NotFound => NotFound(),
            ProjectReopenOutcome.InvalidTransition => Conflict(),
            _ => Ok(new ApiResponse<CompanyProjectDetailResponse>(project!))
        };
    }

    private bool IsCompany() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Company);

    private async Task<Guid?> ResolveCompanyIdAsync(CancellationToken cancellationToken) =>
        await companyService.ResolveCompanyIdAsync(currentUser.UserId!.Value, cancellationToken);

    private ActionResult InvalidCatalog()
    {
        ModelState.AddModelError("skills", "industryId and every skillId must reference an active catalog record.");
        return ValidationProblem(ModelState);
    }

    private ActionResult InvalidTeamSize()
    {
        ModelState.AddModelError("maxTeamSize", "Team size is invalid: minTeamSize <= maxTeamSize and minTeamSize <= expectedStudentCount <= maxTeamSize.");
        return ValidationProblem(ModelState);
    }
}
