using Asp.Versioning;
using DNTU.SkillBridge.Application.Chat;
using DNTU.SkillBridge.Application.Chat.Services;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>Admin kiểm duyệt chat: hàng đợi báo cáo vi phạm, cảnh cáo/khóa quyền nhắn tin, thống kê.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/admin/chat")]
[Authorize]
[Produces("application/json")]
public sealed class AdminChatController(IChatModerationService moderationService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Danh sách báo cáo vi phạm (lọc trạng thái, phân trang).</summary>
    [HttpGet("reports")]
    [ProducesResponseType(typeof(PagedResponse<ChatReportAdminResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ChatReportAdminResponse>>> GetReports(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin()) return Forbid();
        var result = await moderationService.GetReportsAsync(status, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>Thống kê kiểm duyệt cho dashboard Admin.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<ChatModerationStatsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChatModerationStatsResponse>>> GetStats(CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var stats = await moderationService.GetModerationStatsAsync(cancellationToken);
        return Ok(new ApiResponse<ChatModerationStatsResponse>(stats));
    }

    /// <summary>Xử lý báo cáo: DISMISSED bỏ qua / WARNED cảnh cáo / BANNED khóa quyền chat.</summary>
    [HttpPost("reports/{reportId:guid}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> ResolveReport(
        Guid reportId,
        [FromBody] ResolveChatReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var (outcome, error) = await moderationService.ResolveReportAsync(UserId, reportId, request, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<bool>(true)),
            ChatOutcome.MessageNotFound => NotFound(),
            _ => Problem(title: "CHAT_REPORT_INVALID", detail: error, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    /// <summary>Khóa quyền nhắn tin của một tài khoản (không truyền banDays = khóa vĩnh viễn).</summary>
    [HttpPost("users/{targetUserId:guid}/chat-ban")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> BanChat(
        Guid targetUserId,
        [FromBody] SetChatBanRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var (outcome, error) = await moderationService.SetChatBanAsync(targetUserId, request.Reason, request.BanDays, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<bool>(true)),
            ChatOutcome.TargetNotFound => NotFound(),
            _ => Problem(title: "CHAT_BAN_INVALID", detail: error, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    /// <summary>Gỡ khóa quyền nhắn tin.</summary>
    [HttpDelete("users/{targetUserId:guid}/chat-ban")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> UnbanChat(Guid targetUserId, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var (outcome, _) = await moderationService.RemoveChatBanAsync(targetUserId, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<bool>(true)),
            ChatOutcome.TargetNotFound => NotFound(),
            _ => Problem(title: "CHAT_BAN_INVALID", statusCode: StatusCodes.Status400BadRequest),
        };
    }

    private bool IsAdmin() =>
        currentUser.Roles.Contains("ADMIN") || currentUser.Roles.Contains("SUPER_ADMIN");

    private Guid UserId => currentUser.UserId!.Value;

    public sealed record SetChatBanRequest(string? Reason, int? BanDays);
}
