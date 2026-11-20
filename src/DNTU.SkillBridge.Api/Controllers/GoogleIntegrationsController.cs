using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Application.Integrations;
using DNTU.SkillBridge.Infrastructure.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>
/// Google Calendar integration: users connect their Google account once (OAuth consent), after which
/// the backend can create real Google Meet rooms automatically when scheduling meetings/interviews.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/integrations/google")]
[Authorize]
[Produces("application/json")]
public sealed class GoogleIntegrationsController(
    IGoogleMeetLinkService meetLinkService,
    IOptions<GoogleCalendarOptions> calendarOptions,
    IOptions<JwtOptions> jwtOptions,
    IOptions<AppOptions> appOptions,
    ICurrentUser currentUser,
    ILogger<GoogleIntegrationsController> logger) : ControllerBase
{
    private static readonly string NotConfiguredTitle = "GOOGLE_NOT_CONFIGURED";
    private static readonly string NotConnectedTitle = "GOOGLE_NOT_CONNECTED";
    private static readonly string FailedTitle = "GOOGLE_FAILED";

    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponse<GoogleCalendarStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GoogleCalendarStatusResponse>>> Status(CancellationToken cancellationToken)
    {
        var (connected, email) = await meetLinkService.GetStatusAsync(UserId, cancellationToken);
        return Ok(new ApiResponse<GoogleCalendarStatusResponse>(new GoogleCalendarStatusResponse(connected, email)));
    }

    /// <summary>Returns the Google consent URL for the current user. Open it in a new tab.</summary>
    [HttpGet("auth-url")]
    [Authorize(Roles = "COMPANY,LECTURER")]
    [ProducesResponseType(typeof(ApiResponse<GoogleAuthUrlResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<ApiResponse<GoogleAuthUrlResponse>> AuthUrl()
    {
        if (!calendarOptions.Value.IsConfigured) return NotConfigured();
        var url = meetLinkService.BuildAuthorizationUrl(UserId);
        return Ok(new ApiResponse<GoogleAuthUrlResponse>(new GoogleAuthUrlResponse(url, calendarOptions.Value.RedirectUri ?? string.Empty)));
    }

    /// <summary>Google redirects here after consent; exchanges the code and redirects back to the frontend.</summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string state, [FromQuery] string code, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var frontendBase = appOptions.Value.FrontendBaseUrl.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Redirect($"{frontendBase}/company/meetings?google=denied");
        }
        if (!GoogleCalendarService.TryReadState(state, ResolveStateKey(), out var userId))
        {
            logger.LogWarning("Google OAuth callback rejected: invalid or expired state.");
            return Redirect($"{frontendBase}/company/meetings?google=invalid_state");
        }

        var success = await meetLinkService.CompleteAuthorizationAsync(userId, code, cancellationToken: cancellationToken);
        return Redirect($"{frontendBase}/company/meetings?google={(success ? "connected" : "failed")}");
    }

    /// <summary>Creates a real Google Meet room via the connected Google account.</summary>
    [HttpPost("meet-link")]
    [Authorize(Roles = "COMPANY,LECTURER")]
    [ProducesResponseType(typeof(ApiResponse<GoogleMeetLinkResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<GoogleMeetLinkResponse>>> CreateMeetLink([FromBody] GoogleMeetLinkRequest request, CancellationToken cancellationToken)
    {
        if (!calendarOptions.Value.IsConfigured) return NotConfigured();
        var (outcome, link) = await meetLinkService.CreateMeetLinkAsync(UserId, request, cancellationToken);
        return outcome switch
        {
            GoogleLinkOutcome.NotConnected => Conflict(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Title = NotConnectedTitle,
                Detail = "Tài khoản này chưa kết nối Google. Bấm 'Kết nối Google' rồi thử lại sau khi hoàn tất.",
                Status = StatusCodes.Status409Conflict
            }),
            GoogleLinkOutcome.Failed => Conflict(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Title = FailedTitle,
                Detail = "Google từ chối yêu cầu tạo phòng Meet (quyền bị thu hồi hoặc lỗi tạm thời). Hãy thử kết nối lại Google.",
                Status = StatusCodes.Status409Conflict
            }),
            _ when link is not null => Ok(new ApiResponse<GoogleMeetLinkResponse>(link)),
            _ => BadRequest()
        };
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        await meetLinkService.DisconnectAsync(UserId, cancellationToken);
        return NoContent();
    }

    private string ResolveStateKey() =>
        !string.IsNullOrWhiteSpace(calendarOptions.Value.StateSigningKey)
            ? calendarOptions.Value.StateSigningKey
            : jwtOptions.Value.SigningKey;

    private ObjectResult NotConfigured() => Conflict(new Microsoft.AspNetCore.Mvc.ProblemDetails
    {
        Title = NotConfiguredTitle,
        Detail = "Tính năng tạo link Meet tự động chưa được cấu hình trên máy chủ (thiếu GoogleCalendar:ClientId/ClientSecret).",
        Status = StatusCodes.Status409Conflict
    });

    private Guid UserId => currentUser.UserId!.Value;
}
