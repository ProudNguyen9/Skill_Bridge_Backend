using Asp.Versioning;
using DNTU.SkillBridge.Application.Applications;
using DNTU.SkillBridge.Application.Common;
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
[Route("api/v1")]
[Produces("application/json")]
public sealed class ApplicationsController(IApplicationService applicationService, ICurrentUser currentUser) : ControllerBase
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
            ApplicationApplyOutcome.AlreadyApplied => Conflict(new ProblemDetails
            {
                Title = "Không thể nộp hồ sơ",
                Detail = request.TeamId.HasValue
                    ? "Nhóm đã có hồ sơ ứng tuyển hoặc đang thực hiện một dự án chính khác. Chỉ được làm dự án mới sau khi dự án hiện tại kết thúc hoặc rút đơn."
                    : "Bạn đã nộp hồ sơ cho đề tài này rồi.",
                Status = StatusCodes.Status409Conflict
            }),
            ApplicationApplyOutcome.ProjectNotFound => NotFound(),
            ApplicationApplyOutcome.ProjectNotAccepting => Conflict(new ProblemDetails
            {
                Title = "Không thể nộp hồ sơ",
                Detail = "Đề tài không còn nhận hồ sơ hoặc nhóm/thành viên không đủ điều kiện (nhóm đang khóa cho dự án hiện tại hoặc số lượng thành viên chưa phù hợp).",
                Status = StatusCodes.Status409Conflict
            }),
            _ => Forbid() // StudentNotFound — the authenticated user has no student profile.
        };
    }

    /// <summary>Lists the caller's own applications, newest first, with an optional status filter.</summary>
    [HttpGet("applications/me")]
    [Authorize]
    [ProducesResponseType(typeof(PagedResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<ApplicationResponse>>> ListMine(
        [FromQuery] ApplicationListQuery query, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        return Ok(await applicationService.ListMyApplicationsAsync(currentUser.UserId!.Value, query, cancellationToken));
    }

    /// <summary>Lists team applications where the caller is listed as a team member requiring consent or tracking (Bước 15).</summary>
    [HttpGet("applications/me/group-consents")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<ApplicationResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ApplicationResponse>>>> ListGroupConsents(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<ApplicationResponse>>(
            await applicationService.ListMyGroupMemberApplicationsAsync(currentUser.UserId!.Value, cancellationToken)));
    }

    /// <summary>Lists individual applications waiting for this team leader's approval to join an accepted team.</summary>
    [HttpGet("applications/me/leader-join-requests")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<ApplicationResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ApplicationResponse>>>> ListLeaderJoinRequests(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<ApplicationResponse>>(
            await applicationService.ListLeaderJoinRequestsAsync(currentUser.UserId!.Value, cancellationToken)));
    }

    /// <summary>Team leader approves or declines adding a company-proposed individual applicant to the existing team.</summary>
    [HttpPost("applications/{applicationId:guid}/leader-join-approval")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> RespondLeaderJoinApproval(
        Guid applicationId, [FromBody] LeaderJoinApprovalRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var (outcome, application) = await applicationService.RespondLeaderJoinApprovalAsync(
            currentUser.UserId!.Value, applicationId, request.Approve, request.Note, cancellationToken);
        return outcome switch
        {
            ApplicationActionOutcome.NotFound => NotFound(),
            ApplicationActionOutcome.Forbidden => Forbid(),
            ApplicationActionOutcome.Conflict => Conflict(new ProblemDetails
            {
                Title = "Không thể thêm thành viên vào nhóm",
                Detail = "Dự án đã đủ chỉ tiêu sinh viên hoặc ứng viên đã tham gia đề tài khác.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => Ok(new ApiResponse<ApplicationResponse>(application!))
        };
    }

    /// <summary>Member confirms or declines sharing their profile in a team application (Bước 15).</summary>
    [HttpPost("applications/{applicationId:guid}/member-consent")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> RespondMemberConsent(
        Guid applicationId, [FromBody] MemberConsentRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var (outcome, application) = await applicationService.RespondMemberConsentAsync(
            currentUser.UserId!.Value, applicationId, request.Approve, request.Note, cancellationToken);
        return outcome switch
        {
            ApplicationActionOutcome.NotFound => NotFound(),
            ApplicationActionOutcome.Forbidden => Forbid(),
            ApplicationActionOutcome.Conflict => Conflict(),
            _ => Ok(new ApiResponse<ApplicationResponse>(application!))
        };
    }

    /// <summary>Team leader submits a prepared team application once all members have confirmed consent (Bước 16).</summary>
    [HttpPost("applications/{applicationId:guid}/submit")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> SubmitTeamApplication(
        Guid applicationId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var (outcome, application) = await applicationService.SubmitTeamApplicationAsync(currentUser.UserId!.Value, applicationId, cancellationToken);
        return outcome switch
        {
            ApplicationActionOutcome.NotFound => NotFound(),
            ApplicationActionOutcome.Forbidden => Forbid(),
            ApplicationActionOutcome.Conflict => Conflict(),
            _ => Ok(new ApiResponse<ApplicationResponse>(application!))
        };
    }

    /// <summary>Team leader confirms company's adjustment proposal on team size/roles (Bước 21).</summary>
    [HttpPost("applications/{applicationId:guid}/confirm-adjustment")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> ConfirmAdjustment(
        Guid applicationId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var (outcome, application) = await applicationService.ConfirmAdjustmentAsync(currentUser.UserId!.Value, applicationId, cancellationToken);
        return outcome switch
        {
            ApplicationActionOutcome.NotFound => NotFound(),
            ApplicationActionOutcome.Forbidden => Forbid(),
            ApplicationActionOutcome.Conflict => Conflict(),
            _ => Ok(new ApiResponse<ApplicationResponse>(application!))
        };
    }

    /// <summary>Checks for overlapping individual vs team applications on a project for the current student (Bước 18).</summary>
    [HttpGet("projects/{projectId:guid}/application-conflicts")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<ApplicationConflictItemResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ApplicationConflictItemResponse>>>> CheckConflicts(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<ApplicationConflictItemResponse>>(
            await applicationService.CheckConflictsForProjectAsync(currentUser.UserId!.Value, projectId, cancellationToken)));
    }

    /// <summary>Resolves duplicate/conflicting individual vs team application priority (Bước 18).</summary>
    [HttpPost("applications/{applicationId:guid}/resolve-conflict")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> ResolveConflict(
        Guid applicationId, [FromBody] ResolveApplicationConflictRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var (outcome, application) = await applicationService.ResolveConflictAsync(currentUser.UserId!.Value, applicationId, request, cancellationToken);
        return outcome switch
        {
            ApplicationActionOutcome.NotFound => NotFound(),
            ApplicationActionOutcome.Forbidden => Forbid(),
            ApplicationActionOutcome.Conflict => Conflict(),
            _ => Ok(new ApiResponse<ApplicationResponse>(application!))
        };
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
