using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.SePay;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1")]
[Produces("application/json")]
public sealed class SePayController(ISePayService sePayService, ICurrentUser currentUser) : ControllerBase
{
    [Authorize]
    [HttpPost("company/funding-orders/{fundingOrderId:guid}/checkout")]
    [ProducesResponseType(typeof(ApiResponse<SePayCheckoutResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SePayCheckoutResponse>>> Checkout(Guid fundingOrderId, CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Company)) return Forbid();
        var checkout = await sePayService.CreateCheckoutAsync(currentUser.UserId!.Value, fundingOrderId, cancellationToken);
        return checkout is null ? Conflict() : Ok(new ApiResponse<SePayCheckoutResponse>(checkout));
    }

    [AllowAnonymous]
    [HttpPost("integrations/sepay/ipn")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ipn(SePayIpnRequest request, CancellationToken cancellationToken) =>
        await sePayService.ApplyIpnAsync(request, cancellationToken) ? NoContent() : BadRequest();
}
