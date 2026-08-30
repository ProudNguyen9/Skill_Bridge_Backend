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
[Route("api/v{version:apiVersion}")]
[Authorize]
[Produces("application/json")]
public sealed class CommitmentsController(ICommitmentService commitmentService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("projects/{projectId:guid}/commitment")]
    [ProducesResponseType(typeof(ApiResponse<CommitmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CommitmentResponse>>> Get(Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var commitment = await commitmentService.GetAsync(UserId, projectId, cancellationToken);
        return commitment is null ? NotFound() : Ok(new ApiResponse<CommitmentResponse>(commitment));
    }

    [HttpPost("projects/{projectId:guid}/commitment/confirm")]
    [ProducesResponseType(typeof(ApiResponse<CommitmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CommitmentResponse>>> Confirm(Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var commitment = await commitmentService.ConfirmAsync(UserId, projectId, cancellationToken);
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
            _ => CreatedAtAction(nameof(GetWithdrawal), new { withdrawalId = withdrawal!.Id, version = "1" }, new ApiResponse<WithdrawalResponse>(withdrawal))
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
    private bool IsLecturer() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Lecturer);
    private bool IsAdmin() => currentUser.IsAuthenticated && (currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin));
    private Guid UserId => currentUser.UserId!.Value;
}
