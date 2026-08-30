using Asp.Versioning;
using DNTU.SkillBridge.Api.Contracts;
using DNTU.SkillBridge.Api.Applications;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>
/// Student-side application endpoints: apply to a project, list/detail the caller's own
/// applications, and withdraw a pending one. Company review belongs to Task 18.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Produces("application/json")]
public sealed class ApplicationsController(ApplicationService applicationService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Applies the authenticated student to a publicly visible project; duplicate applications answer 409.</summary>
    [HttpPost("projects/{projectId:guid}/applications")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> ApplyToProject(
        Guid projectId, CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        var (outcome, application) = await applicationService.ApplyToProjectAsync(
            currentUser.UserId!.Value, projectId, request, cancellationToken);
        return outcome switch
        {
            ApplicationApplyOutcome.Applied when application is not null =>
                Created(string.Empty, new ApiResponse<ApplicationResponse>(application)),
            ApplicationApplyOutcome.AlreadyApplied => Conflict(),
            ApplicationApplyOutcome.ProjectNotFound => NotFound(),
            ApplicationApplyOutcome.ProjectNotAccepting => Conflict(),
            _ => Forbid() // StudentNotFound — the authenticated user has no student profile.
        };
    }

    /// <summary>Lists the caller's own applications, newest first, with an optional status filter.</summary>
    [HttpGet("applications/me")]
    [Authorize]
    [ProducesResponseType(typeof(Contracts.PagedResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Contracts.PagedResponse<ApplicationResponse>>> ListMine(
        [FromQuery] ApplicationListQuery query, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        return Ok(await applicationService.ListMyApplicationsAsync(currentUser.UserId!.Value, query, cancellationToken));
    }

    /// <summary>Returns one of the caller's own applications; other students' applications answer 404.</summary>
    [HttpGet("applications/{applicationId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> GetMine(
        Guid applicationId, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        var application = await applicationService.GetMyApplicationAsync(currentUser.UserId!.Value, applicationId, cancellationToken);
        return application is null
            ? NotFound()
            : Ok(new ApiResponse<ApplicationResponse>(application));
    }

    /// <summary>Withdraws one of the caller's own PENDING applications; non-pending states answer 409.</summary>
    [HttpDelete("applications/{applicationId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> Withdraw(
        Guid applicationId, WithdrawApplicationRequest? request, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        var (outcome, application) = await applicationService.WithdrawMyApplicationAsync(
            currentUser.UserId!.Value, applicationId, request?.WithdrawReason, cancellationToken);
        return outcome switch
        {
            ApplicationWithdrawOutcome.Withdrawn when application is not null =>
                Ok(new ApiResponse<ApplicationResponse>(application)),
            ApplicationWithdrawOutcome.NotWithdrawable => Conflict(),
            _ => NotFound()
        };
    }

    private bool IsStudent() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Student);
}
