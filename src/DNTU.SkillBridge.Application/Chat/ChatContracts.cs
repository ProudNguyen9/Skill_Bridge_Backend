using DNTU.SkillBridge.Domain.Chat;

namespace DNTU.SkillBridge.Application.Chat;

// ---------- DTO trả về cho FE ----------

public sealed record ChatUserSummary(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string? Role,
    bool IsOnline,
    DateTimeOffset? LastSeenAt);

public sealed record ChatProjectSummary(Guid ProjectId, string Title);

/// <summary>Thẻ ngữ cảnh đính kèm tin nhắn (Dự án / Lịch họp / Đơn ứng tuyển / Biên bản).</summary>
public sealed record ChatMessageContextCard(
    string CardType,
    Guid EntityId,
    string Title,
    string? Subtitle,
    string? LinkUrl,
    string? Extra);

public sealed record ChatMessageReactionSummary(string Emoji, int Count, IReadOnlyCollection<Guid> UserIds);

public sealed record ChatMessageSummary(Guid Id, string Preview, Guid SenderUserId, string SenderDisplayName, string MessageType, DateTimeOffset CreatedAt);

public sealed record ChatMessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string SenderDisplayName,
    string? SenderAvatarUrl,
    string MessageType,
    string Content,
    string? MetadataJson,
    Guid? ReplyToMessageId,
    string? ReplyToPreview,
    string? ReplyToSenderName,
    bool IsEdited,
    DateTimeOffset? EditedAt,
    bool IsDeleted,
    IReadOnlyCollection<ChatMessageReactionSummary> Reactions,
    DateTimeOffset CreatedAt);

public sealed record ChatConversationResponse(
    Guid Id,
    string Type,
    string? Title,
    Guid? ProjectId,
    ChatProjectSummary? Project,
    string? AvatarUrl,
    ChatUserSummary? OtherParticipant,
    ChatMessageSummary? LastMessage,
    int UnreadCount,
    bool IsMuted,
    bool IsPinned,
    string? RequestStatus,
    int ParticipantsCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt);

public sealed record ParticipantReadState(Guid UserId, Guid? LastReadMessageId, DateTimeOffset? LastReadAt);

public sealed record ChatConversationDetailResponse(
    Guid Id,
    string Type,
    string? Title,
    Guid? ProjectId,
    ChatProjectSummary? Project,
    IReadOnlyCollection<ChatUserSummary> Participants,
    IReadOnlyCollection<ParticipantReadState> ReadStates,
    int OnlineCount);

public sealed record ChatMessagesPageResponse(
    Guid ConversationId,
    IReadOnlyCollection<ChatMessageResponse> Messages,
    bool HasMore,
    IReadOnlyCollection<ParticipantReadState> ReadStates);

/// <summary>Người dùng tìm thấy khi tìm kiếm để bắt đầu hội thoại mới.</summary>
public sealed record ChatDirectoryUser(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    string? Detail,
    bool IsOnline,
    Guid? ExistingConversationId);

// ---------- Request payload ----------

public sealed record ContextCardPayload(string CardType, Guid EntityId, string Title, string? Subtitle, string? LinkUrl, string? Extra);

public sealed record OpenDirectConversationRequest(Guid TargetUserId, ContextCardPayload? ContextCard);

public sealed record SendMessageRequest(string MessageType, string? Content, string? MetadataJson, Guid? ReplyToMessageId);

public sealed record EditMessageRequest(string Content);

public sealed record ToggleReactionRequest(string Emoji);

public sealed record ChatAttachmentUploadUrlRequest(string FileName, string ContentType, long SizeBytes);

public sealed record ChatAttachmentUploadUrlResponse(string UploadUrl, string StorageKey, string ViewUrl, string Method);

public sealed record ReportMessageRequest(Guid MessageId, string Reason);

// ---------- Realtime delivery ----------

/// <summary>Gói tin giao cho một người nhận kèm số chưa đọc sau khi lưu.</summary>
public sealed record ChatMessageDelivery(Guid UserId, int UnreadCount);

// ---------- Admin ----------

public sealed record ChatReportAdminResponse(
    Guid Id,
    Guid MessageId,
    ChatMessageResponse? Message,
    Guid ReporterUserId,
    string ReporterDisplayName,
    string Reason,
    string Status,
    Guid? ResolvedByAdminUserId,
    string? ResolutionNote,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt);

public sealed record ResolveChatReportRequest(string Decision, string? Note, int? BanDays);

public sealed record ChatModerationStatsResponse(int OpenReports, int TotalReports, int TotalConversations, int TotalMessages, int BannedUsers);
