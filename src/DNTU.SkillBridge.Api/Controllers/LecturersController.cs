using Asp.Versioning;
using DNTU.SkillBridge.Api.Contracts;
using DNTU.SkillBridge.Api.Lecturers;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>Lecturer self-service endpoints plus the minimal admin supervision operations (full lecturer administration lands in Task 38).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lecturers")]
[Produces("application/json")]
public sealed class LecturersController(LecturerService lecturerService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Returns the caller's own lecturer profile, creating it with defaults (active, private) on first access.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LecturerProfileResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LecturerProfileResponse>>> GetMe(CancellationToken cancellationToken)
    {
        if (!IsLecturer())
        {
            return Forbid();
        }

        return Ok(new ApiResponse<LecturerProfileResponse>(
            await lecturerService.GetMyProfileAsync(currentUser.UserId!.Value, cancellationToken)));
    }

    /// <summary>Updates the caller's own profile; the lecturer id is always taken from the session, never from the payload.</summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LecturerProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LecturerProfileResponse>>> UpdateMe(
        UpdateLecturerProfileRequest request, CancellationToken cancellationToken)
    {
        if (!IsLecturer())
        {
            return Forbid();
        }

        return Ok(new ApiResponse<LecturerProfileResponse>(
            await lecturerService.UpdateMyProfileAsync(currentUser.UserId!.Value, request, cancellationToken)));
    }

    /// <summary>Lists the caller's project supervision assignments.</summary>
    [HttpGet("me/assignments")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>>> GetMyAssignments(CancellationToken cancellationToken)
    {
        if (!IsLecturer())
        {
            return Forbid();
        }

        return Ok(new ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>(
            await lecturerService.GetMyAssignmentsAsync(currentUser.UserId!.Value, cancellationToken)));
    }

    /// <summary>Accepts an invited supervision assignment as the caller's own.</summary>
    [HttpPut("me/assignments/{assignmentId:guid}/accept")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LecturerAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<LecturerAssignmentResponse>>> AcceptAssignment(
        Guid assignmentId, CancellationToken cancellationToken)
    {
        if (!IsLecturer())
        {
            return Forbid();
        }

        var (outcome, assignment) = await lecturerService.AcceptMyAssignmentAsync(currentUser.UserId!.Value, assignmentId, cancellationToken);
        return outcome switch
        {
            LecturerAssignmentAcceptOutcome.NotFound => NotFound(),
            LecturerAssignmentAcceptOutcome.NotOpenForAcceptance => Conflict(),
            _ => Ok(new ApiResponse<LecturerAssignmentResponse>(assignment!))
        };
    }

    /// <summary>Admin: lists a lecturer's project supervision assignments.</summary>
    [HttpGet("{lecturerId:guid}/assignments")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>>> GetLecturerAssignments(
        Guid lecturerId, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        return Ok(new ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>(
            await lecturerService.GetLecturerAssignmentsAsync(lecturerId, cancellationToken)));
    }

    /// <summary>Admin: invites a lecturer to supervise a project; only active lecturers qualify and the project must not be assigned yet.</summary>
    [HttpPost("{lecturerId:guid}/assignments")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LecturerAssignmentResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<LecturerAssignmentResponse>>> CreateAssignment(
        Guid lecturerId, CreateLecturerAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var (outcome, assignment) = await lecturerService.CreateAssignmentAsync(lecturerId, request, cancellationToken);
        return outcome switch
        {
            LecturerAssignmentCreateOutcome.LecturerNotFound => NotFound(),
            LecturerAssignmentCreateOutcome.LecturerInactive => Conflict(),
            LecturerAssignmentCreateOutcome.DuplicateAssignment => Conflict(),
            _ => Created("", new ApiResponse<LecturerAssignmentResponse>(assignment!))
        };
    }

    /// <summary>Admin: removes a supervision assignment.</summary>
    [HttpDelete("assignments/{assignmentId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAssignment(Guid assignmentId, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        return await lecturerService.DeleteAssignmentAsync(assignmentId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    private bool IsLecturer() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Lecturer);

    private bool IsAdmin() => currentUser.IsAuthenticated
        && (currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin));
}
