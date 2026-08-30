using Asp.Versioning;
using DNTU.SkillBridge.Api.Contracts;
using DNTU.SkillBridge.Api.Notifications;
using DNTU.SkillBridge.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}"), Authorize, Produces("application/json")]
public sealed class NotificationsController(NotificationService notificationService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("notifications")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<NotificationResponse>>>> List(CancellationToken ct) => Ok(new ApiResponse<IReadOnlyCollection<NotificationResponse>>(await notificationService.ListAsync(UserId, ct)));
    [HttpGet("notifications/{id:guid}")] public async Task<ActionResult<ApiResponse<NotificationResponse>>> Get(Guid id, CancellationToken ct) { var result = await notificationService.GetAsync(UserId, id, ct); return result is null ? NotFound() : Ok(new ApiResponse<NotificationResponse>(result)); }
    [HttpGet("notifications/unread-count")] public async Task<ActionResult<ApiResponse<int>>> UnreadCount(CancellationToken ct) => Ok(new ApiResponse<int>(await notificationService.UnreadCountAsync(UserId, ct)));
    [HttpPatch("notifications/{id:guid}/read")] public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct) => await notificationService.MarkReadAsync(UserId, id, ct) ? NoContent() : NotFound();
    [HttpPost("notifications/read-all")] public async Task<IActionResult> MarkAllRead(CancellationToken ct) { await notificationService.MarkAllReadAsync(UserId, ct); return NoContent(); }
    [HttpDelete("notifications/{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => await notificationService.DeleteAsync(UserId, id, ct) ? NoContent() : NotFound();
    [HttpGet("notification-preferences")] public async Task<ActionResult<ApiResponse<NotificationPreferenceResponse>>> GetPreferences(CancellationToken ct) => Ok(new ApiResponse<NotificationPreferenceResponse>(await notificationService.GetPreferencesAsync(UserId, ct)));
    [HttpPut("notification-preferences")] public async Task<ActionResult<ApiResponse<NotificationPreferenceResponse>>> UpdatePreferences(UpdateNotificationPreferenceRequest request, CancellationToken ct) => Ok(new ApiResponse<NotificationPreferenceResponse>(await notificationService.UpdatePreferencesAsync(UserId, request, ct)));
    private Guid UserId => currentUser.UserId!.Value;
}