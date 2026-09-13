using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Payments;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

public sealed record CreateFundingOrderRequest(long Amount);
public sealed record FundingOrderResponse(Guid Id, Guid ProjectId, string InvoiceCode, long Amount, string Currency, string Status, DateTimeOffset ExpiresAt);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1")]
[Authorize]
[Produces("application/json")]
public sealed class PaymentsController(IPaymentService paymentService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("company/projects/{projectId:guid}/funding-orders")]
    public async Task<ActionResult<ApiResponse<FundingOrderResponse>>> CreateFundingOrder(
        Guid projectId,
        CreateFundingOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Company)) return Forbid();
        var order = await paymentService.CreateFundingOrderAsync(currentUser.UserId!.Value, projectId, request.Amount, cancellationToken);
        return order is null
            ? BadRequest()
            : Created(string.Empty, new ApiResponse<FundingOrderResponse>(new FundingOrderResponse(order.Id, order.ProjectId, order.InvoiceCode, order.Amount, order.Currency, order.Status.ToString(), order.ExpiresAt)));
    }

    [HttpPost("admin/disbursements/{disbursementId:guid}/start-processing")]
    public async Task<IActionResult> StartDisbursementProcessing(Guid disbursementId, CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Admin)) return Forbid();
        return await paymentService.StartDisbursementProcessingAsync(disbursementId, cancellationToken) ? NoContent() : Conflict();
    }

    [HttpPost("admin/disbursements/{disbursementId:guid}/mark-paid")]
    public async Task<IActionResult> MarkDisbursementPaid(
        Guid disbursementId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromQuery] string bankReference,
        CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Admin) || string.IsNullOrWhiteSpace(idempotencyKey)) return Forbid();
        return await paymentService.MarkDisbursementPaidAsync(disbursementId, idempotencyKey, bankReference, cancellationToken) ? NoContent() : Conflict();
    }

    [HttpPost("admin/disbursements/{disbursementId:guid}/mark-failed")]
    public async Task<IActionResult> MarkDisbursementFailed(Guid disbursementId, [FromQuery] string? note, CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Admin)) return Forbid();
        return await paymentService.MarkDisbursementFailedAsync(disbursementId, note, cancellationToken) ? NoContent() : Conflict();
    }

    [HttpPost("admin/disbursements/{disbursementId:guid}/cancel")]
    public async Task<IActionResult> CancelDisbursement(Guid disbursementId, [FromQuery] string? note, CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Admin)) return Forbid();
        return await paymentService.CancelDisbursementAsync(disbursementId, note, cancellationToken) ? NoContent() : Conflict();
    }
}
