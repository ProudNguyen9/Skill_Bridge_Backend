using Asp.Versioning;
using DNTU.SkillBridge.Application.Applications;
using DNTU.SkillBridge.Application.Companies;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Application.Meetings;
using DNTU.SkillBridge.Domain.Applications;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/company/applications")]
[Authorize]
[Produces("application/json")]
public sealed class CompanyApplicationsController(IApplicationService applicationService, ICompanyService companyService, IMeetingService meetingService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CompanyApplicationResponse>>> List([FromQuery] CompanyApplicationListQuery query, CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdAsync(cancellationToken);
        return companyId is null ? Forbid() : Ok(await applicationService.ListCompanyApplicationsAsync(companyId.Value, query, cancellationToken));
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CompanyProjectApplicationSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CompanyProjectApplicationSummaryResponse>>>> Summary(CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdAsync(cancellationToken);
        return companyId is null ? Forbid() : Ok(new ApiResponse<IReadOnlyCollection<CompanyProjectApplicationSummaryResponse>>(
            await applicationService.GetCompanyApplicationSummaryAsync(companyId.Value, cancellationToken)));
    }

    [HttpGet("{applicationId:guid}")]
    public async Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> Get(Guid applicationId, CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdAsync(cancellationToken);
        if (companyId is null) return Forbid();
        var application = await applicationService.GetCompanyApplicationAsync(companyId.Value, applicationId, cancellationToken);
        return application is null ? NotFound() : Ok(new ApiResponse<CompanyApplicationResponse>(application));
    }

    /// <summary>Places a pending application on the company shortlist.</summary>
    [HttpPost("{applicationId:guid}/shortlist")]
    [ProducesResponseType(typeof(ApiResponse<CompanyApplicationResponse>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> Shortlist(Guid applicationId, [FromBody] ApplicationDecisionRequest? request, CancellationToken cancellationToken) =>
        Decide(applicationId, ApplicationStatus.SHORTLISTED, request, cancellationToken);

    /// <summary>Schedules (or reschedules) the online interview meeting for an application — the candidate joins without being a project member.</summary>
    [HttpPost("{applicationId:guid}/interview-meeting")]
    [ProducesResponseType(typeof(ApiResponse<MeetingResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<MeetingResponse>>> CreateInterviewMeeting(Guid applicationId, [FromBody] CreateInterviewMeetingRequest request, CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdAsync(cancellationToken);
        if (companyId is null) return Forbid();
        var (outcome, meeting) = await meetingService.CreateOrRescheduleInterviewAsync(currentUser.UserId!.Value, companyId.Value, applicationId, request, cancellationToken);
        return outcome switch
        {
            MeetingOutcome.NotFound => NotFound(),
            MeetingOutcome.Forbidden => Forbid(),
            MeetingOutcome.Invalid => Conflict(new ProblemDetails
            {
                Title = "Không thể đặt lịch phỏng vấn",
                Detail = "Đơn ứng tuyển không còn ở trạng thái chờ/phỏng vấn, giờ phỏng vấn phải trong tương lai, hoặc link họp không hợp lệ (chỉ nhận Google Meet, Microsoft Teams, Zoom).",
                Status = StatusCodes.Status409Conflict
            }),
            _ => Created($"meetings/{meeting!.Id}", new ApiResponse<MeetingResponse>(meeting))
        };
    }

    /// <summary>Requests adjustment on team composition/roles or additional info (Bước 20 &amp; 21).</summary>
    [HttpPost("{applicationId:guid}/request-adjustment")]
    [ProducesResponseType(typeof(ApiResponse<CompanyApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> RequestAdjustment(
        Guid applicationId, [FromBody] RequestAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdAsync(cancellationToken);
        if (companyId is null) return Forbid();
        var (outcome, application) = await applicationService.RequestAdjustmentAsync(
            companyId.Value, currentUser.UserId!.Value, applicationId, request, cancellationToken);
        return outcome switch
        {
            ApplicationActionOutcome.NotFound => NotFound(),
            ApplicationActionOutcome.Forbidden => Forbid(),
            ApplicationActionOutcome.Conflict => Conflict(),
            _ => Ok(new ApiResponse<CompanyApplicationResponse>(application!))
        };
    }

    /// <summary>Rejects an active application and retains the optional decision reason.</summary>
    [HttpPost("{applicationId:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<CompanyApplicationResponse>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> Reject(Guid applicationId, [FromBody] ApplicationDecisionRequest? request, CancellationToken cancellationToken) =>
        Decide(applicationId, ApplicationStatus.REJECTED, request, cancellationToken);

    /// <summary>Atomically accepts an application and creates its pending student commitments.</summary>
    [HttpPost("{applicationId:guid}/accept")]
    [ProducesResponseType(typeof(ApiResponse<CompanyApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> Accept(Guid applicationId, [FromBody] ApplicationDecisionRequest? request, CancellationToken cancellationToken) =>
        Decide(applicationId, ApplicationStatus.ACCEPTED, request, cancellationToken);

    /// <summary>Proposes adding an individual applicant to the project's existing accepted team (requires team leader approval).</summary>
    [HttpPost("{applicationId:guid}/request-join-team")]
    [ProducesResponseType(typeof(ApiResponse<CompanyApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> RequestJoinTeam(
        Guid applicationId, [FromBody] RequestJoinTeamRequest request, CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdAsync(cancellationToken);
        if (companyId is null) return Forbid();
        var (outcome, application) = await applicationService.RequestJoinTeamAsync(
            companyId.Value, currentUser.UserId!.Value, applicationId, request, cancellationToken);
        return outcome switch
        {
            ApplicationActionOutcome.NotFound => NotFound(),
            ApplicationActionOutcome.Forbidden => Forbid(),
            ApplicationActionOutcome.Conflict => Conflict(new ProblemDetails
            {
                Title = "Không thể đề xuất thêm vào nhóm",
                Detail = "Dự án chưa có nhóm tiếp nhận, đã hết chỉ tiêu sinh viên, hoặc ứng viên này đã có nhóm/trúng tuyển ở đề tài khác.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => Ok(new ApiResponse<CompanyApplicationResponse>(application!))
        };
    }

    /// <summary>Merges multiple individual applications into a project team and issues commitments (Bước 22).</summary>
    [HttpPost("/api/v1/company/projects/{projectId:guid}/merge-applications")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CompanyApplicationResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CompanyApplicationResponse>>>> MergeApplications(
        Guid projectId, [FromBody] MergeIndividualApplicationsRequest request, CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdAsync(cancellationToken);
        if (companyId is null) return Forbid();
        var (outcome, applications) = await applicationService.MergeIndividualApplicationsAsync(
            companyId.Value, currentUser.UserId!.Value, projectId, request, cancellationToken);
        return outcome switch
        {
            ApplicationActionOutcome.NotFound => NotFound(),
            ApplicationActionOutcome.Forbidden => Forbid(),
            ApplicationActionOutcome.Conflict => Conflict(new ProblemDetails
            {
                Title = "Không thể ghép nhóm ứng viên",
                Detail = "Một hoặc nhiều sinh viên được chọn đã trúng tuyển / đang tham gia đề tài khác, hoặc số lượng thành viên vượt quá chỉ tiêu còn lại của bài toán.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => Ok(new ApiResponse<IReadOnlyCollection<CompanyApplicationResponse>>(applications))
        };
    }

    private async Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> Decide(Guid applicationId, ApplicationStatus status, ApplicationDecisionRequest? request, CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdAsync(cancellationToken);
        if (companyId is null) return Forbid();
        var (outcome, application) = await applicationService.DecideCompanyApplicationAsync(companyId.Value, currentUser.UserId!.Value, applicationId, status, request, cancellationToken);
        return outcome switch
        {
            ApplicationWithdrawOutcome.Withdrawn when application is not null => Ok(new ApiResponse<CompanyApplicationResponse>(application)),
            ApplicationWithdrawOutcome.NotFound => NotFound(),
            _ => Conflict()
        };
    }

    private async Task<Guid?> CompanyIdAsync(CancellationToken cancellationToken) =>
        !currentUser.IsAuthenticated || !currentUser.Roles.Contains(RoleNames.Company)
            ? null
            : await companyService.ResolveCompanyIdAsync(currentUser.UserId!.Value, cancellationToken);
}
