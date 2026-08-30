using Asp.Versioning;
using DNTU.SkillBridge.Api.Contracts;
using DNTU.SkillBridge.Api.Submissions;
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
public sealed class SubmissionsController(SubmissionService submissionService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Creates the first immutable evidence version for an active project member.</summary>
    [HttpPost("projects/{projectId:guid}/submissions")]
    [ProducesResponseType(typeof(ApiResponse<SubmissionResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<SubmissionResponse>>> Create(Guid projectId, [FromBody] CreateSubmissionRequest request, CancellationToken cancellationToken)
    {
        if (!CanCreate()) return Forbid();
        var (outcome, submission) = await submissionService.CreateAsync(UserId, projectId, request, cancellationToken);
        return outcome switch
        {
            SubmissionOutcome.Success when submission is not null => Created($"submissions/{submission.Id}", new ApiResponse<SubmissionResponse>(submission)),
            SubmissionOutcome.Forbidden => Forbid(),
            SubmissionOutcome.Invalid => BadRequest(),
            _ => Conflict()
        };
    }

    /// <summary>Returns a submission only if the caller is a project student, owning company, or assigned lecturer.</summary>
    [HttpGet("submissions/{submissionId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SubmissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SubmissionResponse>>> Get(Guid submissionId, CancellationToken cancellationToken)
    {
        var (outcome, submission) = await submissionService.GetAsync(UserId, submissionId, cancellationToken);
        return outcome switch
        {
            SubmissionOutcome.Success when submission is not null => Ok(new ApiResponse<SubmissionResponse>(submission)),
            SubmissionOutcome.Forbidden => Forbid(),
            _ => NotFound()
        };
    }

    /// <summary>Appends an immutable evidence version using the current optimistic-concurrency token.</summary>
    [HttpPost("submissions/{submissionId:guid}/new-version")]
    [ProducesResponseType(typeof(ApiResponse<SubmissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<SubmissionResponse>>> NewVersion(Guid submissionId, [FromBody] CreateSubmissionVersionRequest request, CancellationToken cancellationToken)
    {
        if (!CanCreate()) return Forbid();
        var (outcome, submission) = await submissionService.NewVersionAsync(UserId, submissionId, request, cancellationToken);
        return outcome switch
        {
            SubmissionOutcome.Success when submission is not null => Ok(new ApiResponse<SubmissionResponse>(submission)),
            SubmissionOutcome.Forbidden => Forbid(),
            SubmissionOutcome.NotFound => NotFound(),
            SubmissionOutcome.Invalid => BadRequest(),
            _ => Conflict()
        };
    }

    [HttpGet("submissions/{submissionId:guid}/versions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<SubmissionVersionResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SubmissionVersionResponse>>>> Versions(Guid submissionId, CancellationToken cancellationToken)
    {
        var (outcome, versions) = await submissionService.ListVersionsAsync(UserId, submissionId, cancellationToken);
        return outcome switch
        {
            SubmissionOutcome.Success when versions is not null => Ok(new ApiResponse<IReadOnlyCollection<SubmissionVersionResponse>>(versions)),
            SubmissionOutcome.Forbidden => Forbid(),
            _ => NotFound()
        };
    }

    [HttpGet("submissions/{submissionId:guid}/history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<SubmissionStatusHistoryResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SubmissionStatusHistoryResponse>>>> History(Guid submissionId, CancellationToken cancellationToken)
    {
        var (outcome, history) = await submissionService.ListHistoryAsync(UserId, submissionId, cancellationToken);
        return outcome switch
        {
            SubmissionOutcome.Success when history is not null => Ok(new ApiResponse<IReadOnlyCollection<SubmissionStatusHistoryResponse>>(history)),
            SubmissionOutcome.Forbidden => Forbid(),
            _ => NotFound()
        };
    }

    [HttpGet("students/me/submissions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<SubmissionResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SubmissionResponse>>>> ListMine(CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Student)) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<SubmissionResponse>>(await submissionService.ListStudentAsync(UserId, cancellationToken)));
    }

    [HttpGet("company/submissions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<SubmissionResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SubmissionResponse>>>> ListCompany(CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Company)) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<SubmissionResponse>>(await submissionService.ListCompanyAsync(UserId, cancellationToken)));
    }

    [HttpGet("lecturer/submissions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<SubmissionResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SubmissionResponse>>>> ListLecturer(CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Lecturer)) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<SubmissionResponse>>(await submissionService.ListLecturerAsync(UserId, cancellationToken)));
    }

    private bool CanCreate() => currentUser.Roles.Contains(RoleNames.Student) &&
        (currentUser.Permissions.Contains(PermissionNames.SubmissionsCreate) || currentUser.Permissions.Count == 0);
    private Guid UserId => currentUser.UserId!.Value;
}
