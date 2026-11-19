using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Lecturers;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Application.Teams;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>Lecturer self-service endpoints plus the minimal admin supervision operations (full lecturer administration lands in Task 38).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/lecturers")]
[Produces("application/json")]
public sealed class LecturersController(ILecturerService lecturerService, ICurrentUser currentUser) : ControllerBase
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

    /// <summary>Declines an invited supervision assignment with an optional reason.</summary>
    [HttpPut("me/assignments/{assignmentId:guid}/decline")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LecturerAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<LecturerAssignmentResponse>>> DeclineAssignment(
        Guid assignmentId, [FromBody] DeclineLecturerAssignmentRequest? request, CancellationToken cancellationToken)
    {
        if (!IsLecturer())
        {
            return Forbid();
        }

        var (outcome, assignment) = await lecturerService.DeclineMyAssignmentAsync(currentUser.UserId!.Value, assignmentId, request?.Reason, cancellationToken);
        return outcome switch
        {
            LecturerAssignmentAcceptOutcome.NotFound => NotFound(),
            LecturerAssignmentAcceptOutcome.NotOpenForAcceptance => Conflict(),
            _ => Ok(new ApiResponse<LecturerAssignmentResponse>(assignment!))
        };
    }

    /// <summary>Searches active, admin-approved lecturers so teams and companies can invite a technical advisor.</summary>
    [HttpGet("directory")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<LecturerDirectoryItemResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<LecturerDirectoryItemResponse>>>> SearchDirectory(
        [FromQuery] string? q = null, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        return Ok(new ApiResponse<IReadOnlyCollection<LecturerDirectoryItemResponse>>(
            await lecturerService.SearchDirectoryAsync(q, limit, cancellationToken)));
    }

    /// <summary>Team/student/company/admin invites an approved lecturer to provide technical supervision on a project (Bước 27).</summary>
    [HttpPost("/api/v1/projects/{projectId:guid}/lecturer-invitations")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LecturerAssignmentResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<LecturerAssignmentResponse>>> InviteToProject(
        Guid projectId, [FromBody] InviteProjectLecturerRequest request, CancellationToken cancellationToken)
    {
        var (outcome, assignment) = await lecturerService.InviteLecturerToProjectAsync(
            currentUser.UserId!.Value, IsAdmin(), projectId, request, cancellationToken);
        return outcome switch
        {
            ProjectLecturerInviteOutcome.ProjectNotFound or ProjectLecturerInviteOutcome.LecturerNotFound => NotFound(),
            ProjectLecturerInviteOutcome.Forbidden => Forbid(),
            ProjectLecturerInviteOutcome.LecturerInactive => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Giảng viên chưa sẵn sàng nhận hướng dẫn",
                detail: "Hồ sơ giảng viên hiện tạm ngưng hoạt động nên chưa thể nhận lời mời."),
            ProjectLecturerInviteOutcome.DuplicateAssignment => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Dự án đã có lời mời hoặc giảng viên hướng dẫn",
                detail: "Dự án này đang có lời mời chờ giảng viên xác nhận hoặc đã có giảng viên phụ trách. Vui lòng huỷ lời mời đang chờ trước khi mời giảng viên khác."),
            _ => Created(string.Empty, new ApiResponse<LecturerAssignmentResponse>(assignment!))
        };
    }

    /// <summary>Lists all lecturer invitations across projects accessible to the authenticated user (student/company).</summary>
    [HttpGet("/api/v1/lecturers/invitations/sent")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>>> ListSentInvitations(
        CancellationToken cancellationToken)
    {
        return Ok(new ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>(
            await lecturerService.ListSentProjectInvitationsAsync(currentUser.UserId!.Value, cancellationToken)));
    }

    /// <summary>Lists lecturer supervision invitations/assignments for a project.</summary>
    [HttpGet("/api/v1/projects/{projectId:guid}/lecturer-invitations")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>>> ListProjectAssignments(
        Guid projectId, CancellationToken cancellationToken)
    {
        return Ok(new ApiResponse<IReadOnlyCollection<LecturerAssignmentResponse>>(
            await lecturerService.ListProjectAssignmentsAsync(projectId, cancellationToken)));
    }

    /// <summary>Cancels a pending lecturer invitation on a project.</summary>
    [HttpDelete("/api/v1/projects/{projectId:guid}/lecturer-invitations/{assignmentId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelProjectInvitation(Guid projectId, Guid assignmentId, CancellationToken cancellationToken)
    {
        return await lecturerService.CancelProjectInvitationAsync(currentUser.UserId!.Value, IsAdmin(), projectId, assignmentId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>Lecturer: lists unlocked student teams available for project recommendation (Bước 23).</summary>
    [HttpGet("teams")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<LecturerTeamItemResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<LecturerTeamItemResponse>>>> ListTeams(
        [FromQuery] string? q = null, CancellationToken cancellationToken = default)
    {
        if (!IsLecturer()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<LecturerTeamItemResponse>>(
            await lecturerService.ListTeamsForLecturerAsync(q, cancellationToken)));
    }

    /// <summary>Lecturer: recommends a student team to a marketplace project (Bước 24).</summary>
    [HttpPost("recommendations")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TeamProjectRecommendationResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<TeamProjectRecommendationResponse>>> RecommendTeam(
        [FromBody] CreateTeamRecommendationRequest request, CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        var (outcome, recommendation) = await lecturerService.RecommendTeamToProjectAsync(currentUser.UserId!.Value, request, cancellationToken);
        return outcome switch
        {
            LecturerRecommendationCreateOutcome.LecturerNotApproved => Forbid(),
            LecturerRecommendationCreateOutcome.TeamNotFound or LecturerRecommendationCreateOutcome.ProjectNotFound => NotFound(),
            LecturerRecommendationCreateOutcome.DuplicatePending => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Không thể gửi đề xuất cho nhóm này",
                detail: "Nhóm hiện đang tham gia/ứng tuyển một dự án khác hoặc đã có đề xuất tương tự đang chờ xử lý."),
            _ => Created(string.Empty, new ApiResponse<TeamProjectRecommendationResponse>(recommendation!))
        };
    }

    /// <summary>Lecturer: lists team recommendations sent by the current lecturer.</summary>
    [HttpGet("me/recommendations")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<TeamProjectRecommendationResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TeamProjectRecommendationResponse>>>> ListMyRecommendations(CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<TeamProjectRecommendationResponse>>(
            await lecturerService.ListMyRecommendationsAsync(currentUser.UserId!.Value, cancellationToken)));
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
