namespace DNTU.SkillBridge.Application.Chat;

/// <summary>Use case chính của tính năng Chat — controller gọi trực tiếp (không MediatR).</summary>
public interface IChatService
{
    // ----- Hội thoại -----
    Task<(ChatOutcome Outcome, ChatConversationResponse? Conversation, string? ErrorMessage)> OpenDirectConversationAsync(Guid userId, OpenDirectConversationRequest request, CancellationToken cancellationToken);

    /// <summary>Mở chat 1-1 với đại diện doanh nghiệp theo CompanyId (dùng ở trang đề tài / hồ sơ công ty).</summary>
    Task<(ChatOutcome Outcome, ChatConversationResponse? Conversation, string? ErrorMessage)> OpenCompanyConversationAsync(Guid userId, Guid companyId, ContextCardPayload? contextCard, CancellationToken cancellationToken);

    /// <summary>Lấy/tự đồng bộ phòng chat nhóm dự án 3 bên.</summary>
    Task<(ChatOutcome Outcome, ChatConversationResponse? Conversation, string? ErrorMessage)> OpenProjectConversationAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatConversationResponse>> GetConversationsAsync(Guid userId, string? tab, string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<ChatConversationDetailResponse?> GetConversationDetailAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);

    Task<int> GetTotalUnreadCountAsync(Guid userId, CancellationToken cancellationToken);

    Task SetPinnedAsync(Guid userId, Guid conversationId, bool pinned, CancellationToken cancellationToken);

    Task SetMutedAsync(Guid userId, Guid conversationId, bool muted, CancellationToken cancellationToken);

    // ----- Tin nhắn -----
    Task<ChatMessagesPageResponse?> GetMessagesAsync(Guid userId, Guid conversationId, Guid? beforeMessageId, int limit, CancellationToken cancellationToken);

    Task<(ChatOutcome Outcome, ChatMessageResponse? Message, string? ErrorMessage)> SendMessageAsync(Guid userId, Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken);

    Task<(ChatOutcome Outcome, ChatMessageResponse? Message, string? ErrorMessage)> EditMessageAsync(Guid userId, Guid messageId, string content, CancellationToken cancellationToken);

    Task<(ChatOutcome Outcome, ChatMessageResponse? Message)> DeleteMessageAsync(Guid userId, Guid messageId, CancellationToken cancellationToken);

    Task<(ChatOutcome Outcome, ChatMessageResponse? Message, string? ErrorMessage)> ToggleReactionAsync(Guid userId, Guid messageId, string emoji, CancellationToken cancellationToken);

    Task<(ChatOutcome Outcome, ChatMessageResponse? Message)> MarkConversationReadAsync(Guid userId, Guid conversationId, Guid? messageId, CancellationToken cancellationToken);

    Task<ChatOutcome> AcceptConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);

    Task<ChatOutcome> DeclineConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);

    // ----- Đính kèm / Báo cáo / Tìm người -----
    Task<(ChatOutcome Outcome, ChatAttachmentUploadUrlResponse? Upload, string? ErrorMessage)> CreateAttachmentUploadUrlAsync(Guid userId, ChatAttachmentUploadUrlRequest request, CancellationToken cancellationToken);

    Task<(ChatReportOutcome Outcome, string? ErrorMessage)> ReportMessageAsync(Guid userId, ReportMessageRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatDirectoryUser>> SearchPeopleAsync(Guid userId, string? query, CancellationToken cancellationToken);
}
