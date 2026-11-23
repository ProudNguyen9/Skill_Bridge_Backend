using Asp.Versioning;
using DNTU.SkillBridge.Application.Chat;
using DNTU.SkillBridge.Application.Chat.Services;
using DNTU.SkillBridge.Application.Common.Security;
using Microsoft.AspNetCore.RateLimiting;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>REST surface của tính năng Chat (hội thoại, tin nhắn, reaction, đính kèm, báo cáo).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/chat")]
[Authorize]
[Produces("application/json")]
public sealed class ChatController(IChatService chatService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Danh sách hội thoại của người dùng hiện tại (lọc tab, tìm kiếm, phân trang).</summary>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChatConversationResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChatConversationResponse>>>> GetConversations(
        [FromQuery] string? tab,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var conversations = await chatService.GetConversationsAsync(UserId, tab, search, page, pageSize, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<ChatConversationResponse>>(conversations));
    }

    /// <summary>Tổng số tin nhắn chưa đọc (badge topbar).</summary>
    [HttpGet("conversations/unread-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var count = await chatService.GetTotalUnreadCountAsync(UserId, cancellationToken);
        return Ok(new ApiResponse<int>(count));
    }

    /// <summary>Chi tiết hội thoại: thành viên + trạng thái online + trạng thái đã đọc.</summary>
    [HttpGet("conversations/{conversationId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ChatConversationDetailResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChatConversationDetailResponse>>> GetConversationDetail(Guid conversationId, CancellationToken cancellationToken)
    {
        var detail = await chatService.GetConversationDetailAsync(UserId, conversationId, cancellationToken);
        return detail is null ? NotFound() : Ok(new ApiResponse<ChatConversationDetailResponse>(detail));
    }

    /// <summary>Tìm người dùng để bắt đầu hội thoại mới (danh bạ chat).</summary>
    [HttpGet("people")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChatDirectoryUser>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChatDirectoryUser>>>> SearchPeople(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var people = await chatService.SearchPeopleAsync(UserId, q, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<ChatDirectoryUser>>(people));
    }

    /// <summary>Mở/lấy hội thoại 1-1 với một người dùng (kèm thẻ ngữ cảnh tuỳ chọn).</summary>
    [HttpPost("conversations/direct")]
    [EnableRateLimiting("chat-send")]
    [ProducesResponseType(typeof(ApiResponse<ChatConversationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<ChatConversationResponse>>> OpenDirectConversation(
        [FromBody] OpenDirectConversationRequest request,
        CancellationToken cancellationToken)
    {
        var (outcome, conversation, error) = await chatService.OpenDirectConversationAsync(UserId, request, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<ChatConversationResponse>(conversation!)),
            ChatOutcome.RateLimited => Problem(title: "CHAT_RATE_LIMITED", detail: error, statusCode: StatusCodes.Status429TooManyRequests),
            ChatOutcome.BlockedByParticipant => Problem(title: "CHAT_BLOCKED", detail: error, statusCode: StatusCodes.Status403Forbidden),
            ChatOutcome.ChatBanned => Problem(title: "CHAT_BANNED", detail: error, statusCode: StatusCodes.Status403Forbidden),
            ChatOutcome.TargetNotFound => Problem(title: "CHAT_TARGET_NOT_FOUND", detail: error, statusCode: StatusCodes.Status404NotFound),
            ChatOutcome.TargetIsAdmin => Problem(title: "CHAT_TARGET_IS_ADMIN", detail: error, statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(title: "CHAT_INVALID", detail: error, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    /// <summary>Mở chat 1-1 với đại diện doanh nghiệp theo CompanyId (trang đề tài / hồ sơ công ty).</summary>
    [HttpPost("conversations/company/{companyId:guid}")]
    [EnableRateLimiting("chat-send")]
    [ProducesResponseType(typeof(ApiResponse<ChatConversationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChatConversationResponse>>> OpenCompanyConversation(
        Guid companyId,
        [FromBody] OpenCompanyConversationRequest? request,
        CancellationToken cancellationToken)
    {
        var (outcome, conversation, error) = await chatService.OpenCompanyConversationAsync(UserId, companyId, request?.ContextCard, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<ChatConversationResponse>(conversation!)),
            ChatOutcome.TargetNotFound => Problem(title: "CHAT_TARGET_NOT_FOUND", detail: error, statusCode: StatusCodes.Status404NotFound),
            ChatOutcome.BlockedByParticipant => Problem(title: "CHAT_BLOCKED", detail: error, statusCode: StatusCodes.Status403Forbidden),
            ChatOutcome.ChatBanned => Problem(title: "CHAT_BANNED", detail: error, statusCode: StatusCodes.Status403Forbidden),
            ChatOutcome.RateLimited => Problem(title: "CHAT_RATE_LIMITED", detail: error, statusCode: StatusCodes.Status429TooManyRequests),
            _ => Problem(title: "CHAT_INVALID", detail: error, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    /// <summary>Lấy/tự đồng bộ phòng chat nhóm dự án 3 bên.</summary>
    [HttpPost("conversations/project/{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ChatConversationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChatConversationResponse>>> OpenProjectConversation(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var (outcome, conversation, error) = await chatService.OpenProjectConversationAsync(UserId, projectId, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<ChatConversationResponse>(conversation!)),
            ChatOutcome.ProjectNotFound => Problem(title: "CHAT_PROJECT_NOT_FOUND", detail: error, statusCode: StatusCodes.Status404NotFound),
            ChatOutcome.Forbidden => Problem(title: "CHAT_FORBIDDEN", detail: error, statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(title: "CHAT_INVALID", detail: error, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    /// <summary>Lịch sử tin nhắn (cursor pagination theo beforeMessageId).</summary>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessagesPageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChatMessagesPageResponse>>> GetMessages(
        Guid conversationId,
        [FromQuery] Guid? before,
        [FromQuery] int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var page = await chatService.GetMessagesAsync(UserId, conversationId, before, limit, cancellationToken);
        return page is null ? Forbid() : Ok(new ApiResponse<ChatMessagesPageResponse>(page));
    }

    /// <summary>Gửi tin nhắn (Text/Image/File/ContextCard). Rate limit 30 tin/phút.</summary>
    [HttpPost("conversations/{conversationId:guid}/messages")]
    [EnableRateLimiting("chat-send")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<ChatMessageResponse>>> SendMessage(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var (outcome, message, error) = await chatService.SendMessageAsync(UserId, conversationId, request, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<ChatMessageResponse>(message!)),
            ChatOutcome.ConversationNotFound => Problem(title: "CHAT_CONVERSATION_NOT_FOUND", detail: error, statusCode: StatusCodes.Status404NotFound),
            ChatOutcome.Forbidden => Problem(title: "CHAT_FORBIDDEN", detail: error, statusCode: StatusCodes.Status403Forbidden),
            ChatOutcome.NotAccepted => Problem(title: "CHAT_NOT_ACCEPTED", detail: error, statusCode: StatusCodes.Status403Forbidden),
            ChatOutcome.BlockedByParticipant => Problem(title: "CHAT_BLOCKED", detail: error, statusCode: StatusCodes.Status403Forbidden),
            ChatOutcome.ChatBanned => Problem(title: "CHAT_BANNED", detail: error, statusCode: StatusCodes.Status403Forbidden),
            ChatOutcome.RateLimited => Problem(title: "CHAT_RATE_LIMITED", detail: error, statusCode: StatusCodes.Status429TooManyRequests),
            _ => Problem(title: "CHAT_INVALID", detail: error, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    /// <summary>Chỉnh sửa tin nhắn (trong 15 phút, chỉ người gửi).</summary>
    [HttpPatch("messages/{messageId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChatMessageResponse>>> EditMessage(
        Guid messageId,
        [FromBody] EditMessageRequest request,
        CancellationToken cancellationToken)
    {
        var (outcome, message, error) = await chatService.EditMessageAsync(UserId, messageId, request.Content, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<ChatMessageResponse>(message!)),
            ChatOutcome.MessageNotFound => NotFound(),
            ChatOutcome.Forbidden => Problem(title: "CHAT_FORBIDDEN", detail: error, statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(title: "CHAT_INVALID", detail: error, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    /// <summary>Thu hồi tin nhắn (soft-delete, chỉ người gửi).</summary>
    [HttpDelete("messages/{messageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMessage(Guid messageId, CancellationToken cancellationToken)
    {
        var (outcome, _) = await chatService.DeleteMessageAsync(UserId, messageId, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => NoContent(),
            ChatOutcome.MessageNotFound => NotFound(),
            ChatOutcome.Forbidden => Forbid(),
            _ => BadRequest(),
        };
    }

    /// <summary>Bật/tắt reaction trên tin nhắn.</summary>
    [HttpPost("messages/{messageId:guid}/reactions")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChatMessageResponse>>> ToggleReaction(
        Guid messageId,
        [FromBody] ToggleReactionRequest request,
        CancellationToken cancellationToken)
    {
        var (outcome, message, error) = await chatService.ToggleReactionAsync(UserId, messageId, request.Emoji, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<ChatMessageResponse>(message!)),
            ChatOutcome.MessageNotFound => NotFound(),
            ChatOutcome.Forbidden => Problem(title: "CHAT_FORBIDDEN", detail: error, statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(title: "CHAT_INVALID", detail: error, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    /// <summary>Đánh dấu hội thoại đã đọc (LastReadMessageId + broadcast ConversationRead).</summary>
    [HttpPost("conversations/{conversationId:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> MarkRead(
        Guid conversationId,
        [FromBody] MarkConversationReadRequest? request,
        CancellationToken cancellationToken)
    {
        var (outcome, _) = await chatService.MarkConversationReadAsync(UserId, conversationId, request?.MessageId, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<bool>(true)),
            ChatOutcome.ConversationNotFound => NotFound(),
            _ => Forbid(),
        };
    }

    /// <summary>Chấp nhận tin nhắn chờ.</summary>
    [HttpPost("conversations/{conversationId:guid}/accept")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> AcceptConversation(Guid conversationId, CancellationToken cancellationToken)
    {
        var outcome = await chatService.AcceptConversationAsync(UserId, conversationId, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<bool>(true)),
            ChatOutcome.ConversationNotFound => NotFound(),
            _ => Forbid(),
        };
    }

    /// <summary>Từ chối tin nhắn chờ (ẩn hội thoại khỏi hộp thư của người nhận).</summary>
    [HttpPost("conversations/{conversationId:guid}/decline")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> DeclineConversation(Guid conversationId, CancellationToken cancellationToken)
    {
        var outcome = await chatService.DeclineConversationAsync(UserId, conversationId, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<bool>(true)),
            ChatOutcome.ConversationNotFound => NotFound(),
            _ => Forbid(),
        };
    }

    /// <summary>Ghim/bỏ ghim hội thoại.</summary>
    [HttpPost("conversations/{conversationId:guid}/pin")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> SetPinned(
        Guid conversationId,
        [FromBody] SetConversationFlagRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await chatService.SetPinnedAsync(UserId, conversationId, request.Value, cancellationToken);
            return Ok(new ApiResponse<bool>(true));
        }
        catch (InvalidOperationException error)
        {
            return Problem(title: "CHAT_FORBIDDEN", detail: error.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }

    /// <summary>Tắt/bật thông báo hội thoại.</summary>
    [HttpPost("conversations/{conversationId:guid}/mute")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> SetMuted(
        Guid conversationId,
        [FromBody] SetConversationFlagRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await chatService.SetMutedAsync(UserId, conversationId, request.Value, cancellationToken);
            return Ok(new ApiResponse<bool>(true));
        }
        catch (InvalidOperationException error)
        {
            return Problem(title: "CHAT_FORBIDDEN", detail: error.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }

    /// <summary>Lấy presigned upload URL cho ảnh/tệp đính kèm chat (tối đa 15MB).</summary>
    [HttpPost("attachments/upload-url")]
    [ProducesResponseType(typeof(ApiResponse<ChatAttachmentUploadUrlResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChatAttachmentUploadUrlResponse>>> CreateAttachmentUploadUrl(
        [FromBody] ChatAttachmentUploadUrlRequest request,
        CancellationToken cancellationToken)
    {
        var (outcome, upload, error) = await chatService.CreateAttachmentUploadUrlAsync(UserId, request, cancellationToken);
        return outcome switch
        {
            ChatOutcome.Success => Ok(new ApiResponse<ChatAttachmentUploadUrlResponse>(upload!)),
            _ => Problem(title: "CHAT_ATTACHMENT_INVALID", detail: error, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    /// <summary>Báo cáo tin nhắn vi phạm gửi về hàng đợi kiểm duyệt Admin.</summary>
    [HttpPost("reports")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> ReportMessage(
        [FromBody] ReportMessageRequest request,
        CancellationToken cancellationToken)
    {
        var (outcome, error) = await chatService.ReportMessageAsync(UserId, request, cancellationToken);
        return outcome switch
        {
            ChatReportOutcome.Success => Ok(new ApiResponse<bool>(true)),
            ChatReportOutcome.AlreadyReported => Problem(title: "CHAT_REPORT_DUPLICATE", detail: error, statusCode: StatusCodes.Status409Conflict),
            _ => Problem(title: "CHAT_REPORT_INVALID", detail: error, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    private Guid UserId => currentUser.UserId!.Value;

    public sealed record OpenCompanyConversationRequest(ContextCardPayload? ContextCard);

    public sealed record MarkConversationReadRequest(Guid? MessageId);

    public sealed record SetConversationFlagRequest(bool Value);
}
