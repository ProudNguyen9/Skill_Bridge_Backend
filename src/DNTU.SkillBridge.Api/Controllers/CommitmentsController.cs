using System.Text.Json;
using Asp.Versioning;
using DNTU.SkillBridge.Application.Commitments;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1")]
[Authorize]
[Produces("application/json")]
public sealed class CommitmentsController(ICommitmentService commitmentService, ICurrentUser currentUser) : ControllerBase
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpGet("projects/{projectId:guid}/commitment")]
    [ProducesResponseType(typeof(ApiResponse<CommitmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CommitmentResponse>>> Get(Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var commitment = await commitmentService.GetAsync(UserId, projectId, cancellationToken);
        return commitment is null ? NotFound() : Ok(new ApiResponse<CommitmentResponse>(commitment));
    }

    [HttpGet("projects/{projectId:guid}/commitments/overview")]
    [ProducesResponseType(typeof(ApiResponse<ProjectCommitmentOverviewResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProjectCommitmentOverviewResponse>>> GetOverview(Guid projectId, CancellationToken cancellationToken)
    {
        var overview = await commitmentService.GetProjectOverviewForActorAsync(UserId, currentUser.Roles, projectId, cancellationToken);
        return overview is null ? NotFound() : Ok(new ApiResponse<ProjectCommitmentOverviewResponse>(overview));
    }

    [HttpGet("commitments/projects")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<ProjectCommitmentSummaryItemResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ProjectCommitmentSummaryItemResponse>>>> ListProjectCommitments(CancellationToken cancellationToken)
    {
        if (!IsAdmin() && !IsCompany() && !IsLecturer()) return Forbid();
        var items = await commitmentService.ListProjectCommitmentSummariesAsync(UserId, currentUser.Roles, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyCollection<ProjectCommitmentSummaryItemResponse>>(items));
    }

    [HttpPost("projects/{projectId:guid}/commitment/confirm")]
    [ProducesResponseType(typeof(ApiResponse<CommitmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CommitmentResponse>>> Confirm(Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var signRequest = await ReadOptionalSignRequestAsync(cancellationToken);
        var commitment = await commitmentService.ConfirmAsync(UserId, projectId, signRequest, cancellationToken);
        return commitment is null
            ? Conflict(new ProblemDetails
            {
                Title = "Không thể ký cam kết",
                Detail = "Bản cam kết này đã được ký trước đó, đã hết hạn hoặc không hợp lệ.",
                Status = StatusCodes.Status409Conflict
            })
            : Ok(new ApiResponse<CommitmentResponse>(commitment));
    }

    [HttpPost("company/projects/{projectId:guid}/commitment/sign")]
    [ProducesResponseType(typeof(ApiResponse<ProjectCommitmentOverviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ProjectCommitmentOverviewResponse>>> SignCompany(
        Guid projectId, [FromBody] SignCommitmentRequest? request, CancellationToken cancellationToken)
    {
        if (!IsCompany()) return Forbid();
        var (outcome, overview) = await commitmentService.SignCompanyCommitmentAsync(UserId, projectId, request, cancellationToken);
        return outcome switch
        {
            CommitmentSignOutcome.NotFound => NotFound(),
            CommitmentSignOutcome.Forbidden => Forbid(),
            CommitmentSignOutcome.Conflict => Conflict(new ProblemDetails
            {
                Title = "Không thể ký cam kết doanh nghiệp",
                Detail = "Doanh nghiệp đã ký xác nhận cam kết này trước đó hoặc đề tài chưa ở trạng thái hợp lệ.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => Ok(new ApiResponse<ProjectCommitmentOverviewResponse>(overview!))
        };
    }

    [HttpPost("university/projects/{projectId:guid}/commitment/sign")]
    [ProducesResponseType(typeof(ApiResponse<ProjectCommitmentOverviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ProjectCommitmentOverviewResponse>>> SignUniversity(
        Guid projectId, [FromBody] SignCommitmentRequest? request, CancellationToken cancellationToken)
    {
        if (!IsAdmin() && !IsLecturer()) return Forbid();
        var (outcome, overview) = await commitmentService.SignUniversityCommitmentAsync(
            UserId, currentUser.Roles, projectId, request, cancellationToken);
        return outcome switch
        {
            CommitmentSignOutcome.NotFound => NotFound(),
            CommitmentSignOutcome.Forbidden => Forbid(),
            CommitmentSignOutcome.Conflict => Conflict(new ProblemDetails
            {
                Title = "Không thể ký chứng thực Nhà trường",
                Detail = "Đại diện Nhà trường đã ký xác nhận cam kết này trước đó hoặc đề tài chưa ở trạng thái hợp lệ.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => Ok(new ApiResponse<ProjectCommitmentOverviewResponse>(overview!))
        };
    }

    private async Task<SignCommitmentRequest?> ReadOptionalSignRequestAsync(CancellationToken cancellationToken)
    {
        if (!Request.HasJsonContentType() || Request.ContentLength == 0)
        {
            return null;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<SignCommitmentRequest>(
                Request.Body, RequestJsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [HttpPost("projects/{projectId:guid}/commitment/decline")]
    [ProducesResponseType(typeof(ApiResponse<CommitmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CommitmentResponse>>> Decline(
        Guid projectId, [FromBody] DeclineCommitmentRequest? request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var commitment = await commitmentService.DeclineAsync(UserId, projectId, request?.Reason, cancellationToken);
        return commitment is null ? Conflict() : Ok(new ApiResponse<CommitmentResponse>(commitment));
    }

    [HttpGet("students/me/commitments")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CommitmentResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CommitmentResponse>>>> List(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<CommitmentResponse>>(await commitmentService.ListAsync(UserId, cancellationToken)));
    }

    /// <summary>Creates a governed withdrawal request for the caller's current active project membership.</summary>
    [HttpPost("withdrawals")]
    [ProducesResponseType(typeof(ApiResponse<WithdrawalResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<WithdrawalResponse>>> CreateWithdrawal([FromBody] CreateWithdrawalRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var (outcome, withdrawal) = await commitmentService.CreateWithdrawalAsync(UserId, request, cancellationToken);
        return outcome switch
        {
            WithdrawalOutcome.NotFound => NotFound(),
            WithdrawalOutcome.Conflict => Conflict(),
            WithdrawalOutcome.Forbidden => Forbid(),
            _ => CreatedAtAction(nameof(GetWithdrawal), new { withdrawalId = withdrawal!.Id }, new ApiResponse<WithdrawalResponse>(withdrawal))
        };
    }

    [HttpGet("students/me/withdrawals")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<WithdrawalResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<WithdrawalResponse>>>> ListWithdrawals(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<WithdrawalResponse>>(await commitmentService.ListMyWithdrawalsAsync(UserId, cancellationToken)));
    }

    [HttpGet("withdrawals/{withdrawalId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<WithdrawalResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<WithdrawalResponse>>> GetWithdrawal(Guid withdrawalId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var withdrawal = await commitmentService.GetMyWithdrawalAsync(UserId, withdrawalId, cancellationToken);
        return withdrawal is null ? NotFound() : Ok(new ApiResponse<WithdrawalResponse>(withdrawal));
    }

    [HttpPost("withdrawals/{withdrawalId:guid}/recommend")]
    [ProducesResponseType(typeof(ApiResponse<WithdrawalResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WithdrawalResponse>>> RecommendWithdrawal(Guid withdrawalId, [FromBody] WithdrawalDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        var (outcome, withdrawal) = await commitmentService.RecommendAsync(UserId, withdrawalId, request, cancellationToken);
        return ToDecisionResult(outcome, withdrawal);
    }

    [HttpPost("withdrawals/{withdrawalId:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<WithdrawalResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WithdrawalResponse>>> ApproveWithdrawal(Guid withdrawalId, [FromBody] WithdrawalDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var (outcome, withdrawal) = await commitmentService.DecideAsync(UserId, withdrawalId, true, request, cancellationToken);
        return ToDecisionResult(outcome, withdrawal);
    }

    [HttpPost("withdrawals/{withdrawalId:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<WithdrawalResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WithdrawalResponse>>> RejectWithdrawal(Guid withdrawalId, [FromBody] WithdrawalDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var (outcome, withdrawal) = await commitmentService.DecideAsync(UserId, withdrawalId, false, request, cancellationToken);
        return ToDecisionResult(outcome, withdrawal);
    }

    private ActionResult<ApiResponse<WithdrawalResponse>> ToDecisionResult(WithdrawalOutcome outcome, WithdrawalResponse? withdrawal) => outcome switch
    {
        WithdrawalOutcome.NotFound => NotFound(),
        WithdrawalOutcome.Forbidden => Forbid(),
        WithdrawalOutcome.Conflict => Conflict(),
        _ => Ok(new ApiResponse<WithdrawalResponse>(withdrawal!))
    };

    private bool IsStudent() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Student);
    private bool IsCompany() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Company);
    private bool IsLecturer() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Lecturer);
    private bool IsAdmin() => currentUser.IsAuthenticated && (currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin));
    private Guid UserId => currentUser.UserId!.Value;
}
