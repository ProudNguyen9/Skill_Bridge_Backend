using DNTU.SkillBridge.Domain.Chat;
using DNTU.SkillBridge.Domain.Social;

namespace DNTU.SkillBridge.Application.Chat.Repositories;

/// <summary>Tab lọc hộp thư.</summary>
public enum ConversationInboxTab
{
    ALL = 0,
    UNREAD = 1,
    GROUPS = 2,
    REQUESTS = 3,
}

/// <summary>Dòng hội thoại đã project sẵn cho danh sách hộp thư.</summary>
public sealed record ChatConversationRow(
    Guid Id,
    ConversationType Type,
    Guid? ProjectId,
    string? ProjectTitle,
    string? Title,
    string? AvatarUrl,
    ChatMessageSummary? LastMessage,
    int UnreadCount,
    bool IsMuted,
    bool IsPinned,
    ConversationRequestStatus? RequestStatus,
    int ParticipantsCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt,
    Guid? OtherUserId,
    string? OtherDisplayName,
    string? OtherAvatarUrl,
    string? OtherRole);

/// <summary>Dòng tin nhắn đã project (kèm thông tin người gửi + tin nhắn reply).</summary>
public sealed record ChatMessageRow(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string SenderDisplayName,
    string? SenderAvatarUrl,
    ChatMessageType MessageType,
    string Content,
    string? MetadataJson,
    Guid? ReplyToMessageId,
    string? ReplyToPreview,
    string? ReplyToSenderName,
    bool IsEdited,
    DateTimeOffset? EditedAt,
    bool IsDeleted,
    DateTimeOffset CreatedAt);

/// <summary>Cụm reaction gộp theo tin nhắn.</summary>
public sealed record ChatMessageReactionRow(Guid MessageId, string Emoji, int Count, IReadOnlyCollection<Guid> UserIds);

public sealed record ChatDirectoryRow(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    string? Detail,
    Guid? ExistingConversationId);

/// <summary>Thành viên hội thoại cho panel chi tiết (kèm trạng thái đọc).</summary>
public sealed record ChatParticipantRow(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    ConversationParticipantRole ParticipantRole,
    ConversationRequestStatus RequestStatus,
    Guid? LastReadMessageId,
    DateTimeOffset? LastReadAt,
    DateTimeOffset? LeftAt);

public sealed record ChatParticipantReadStateRow(Guid UserId, Guid? LastReadMessageId, DateTimeOffset? LastReadAt);

/// <summary>Thành viên dự án để đồng bộ phòng chat nhóm: company/lecturer/student user ids.</summary>
public sealed record ProjectConversationMembers(IReadOnlyCollection<Guid> CompanyUserIds, IReadOnlyCollection<Guid> LecturerUserIds, IReadOnlyCollection<Guid> StudentUserIds);

/// <summary>Contract persistence cho tính năng chat (hiện thực bằng EF Core ở Infrastructure).</summary>
public interface IChatRepository
{
    // ----- Hội thoại -----
    Task<Conversation?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<Conversation?> GetDirectConversationAsync(Guid userA, Guid userB, CancellationToken cancellationToken);
    Task<Conversation?> GetProjectConversationAsync(Guid projectId, CancellationToken cancellationToken);
    Task<bool> IsParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);
    Task<bool> IsAcceptedParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<(Guid UserId, ConversationRequestStatus Status, DateTimeOffset? LeftAt)>> GetParticipantStatusesAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatParticipantReadStateRow>> GetParticipantReadStatesAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<int> GetUnreadCountAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);
    Task<ChatConversationRow?> GetConversationRowAsync(Guid conversationId, Guid viewerUserId, CancellationToken cancellationToken);

    /// <summary>Thành viên hội thoại kèm thông tin hiển thị (dùng cho panel chi tiết).</summary>
    Task<IReadOnlyList<ChatParticipantRow>> GetConversationParticipantsAsync(Guid conversationId, CancellationToken cancellationToken);

    /// <summary>Thông tin hiển thị cơ bản của một người dùng (tên, avatar, vai trò chính).</summary>
    Task<(Guid UserId, string DisplayName, string? AvatarUrl, string Role)?> GetUserDisplayAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Vai trò chính của người dùng (STUDENT/LECTURER/COMPANY) — dùng để xây URL đích theo portal.</summary>
    Task<string> ResolveProfileRoleAsync(Guid userId, CancellationToken cancellationToken);

    // ----- Hộp thư -----
    Task<IReadOnlyList<ChatConversationRow>> ListConversationsAsync(Guid userId, ConversationInboxTab tab, string? search, int skip, int take, CancellationToken cancellationToken);
    Task<int> CountConversationsAsync(Guid userId, ConversationInboxTab tab, string? search, CancellationToken cancellationToken);
    Task<int> GetTotalUnreadCountAsync(Guid userId, CancellationToken cancellationToken);

    // ----- Tin nhắn -----
    Task<ChatMessage?> GetMessageAsync(Guid messageId, CancellationToken cancellationToken);
    Task<ChatMessageRow?> GetMessageRowAsync(Guid messageId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatMessageRow>> ListMessagesAsync(Guid conversationId, DateTimeOffset? beforeCreatedAt, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatMessageReactionRow>> GetReactionsForMessagesAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken cancellationToken);

    // ----- Quan hệ / chống spam -----
    Task<bool> HasDirectRelationshipAsync(Guid userA, Guid userB, CancellationToken cancellationToken);
    Task<bool> IsBlockedBetweenAsync(Guid userA, Guid userB, CancellationToken cancellationToken);
    Task<int> CountNewPendingRequestsCreatedTodayAsync(Guid userId, DateTimeOffset dayStartUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatDirectoryRow>> SearchDirectoryAsync(Guid userId, string? query, int limit, CancellationToken cancellationToken);
    Task<(Guid UserId, string DisplayName, string? ChatBanReason, DateTimeOffset? ChatBannedUntil)?> GetUserChatStateAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> IsAdminUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> HasOpenReportAsync(Guid messageId, Guid reporterUserId, CancellationToken cancellationToken);
    Task<bool> HasProjectAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);
    Task<Guid?> GetCompanyPrimaryUserAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>Số tin nhắn chưa đọc của TẤT CẢ thành viên còn hoạt động trong hội thoại.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetUnreadCountsForConversationAsync(Guid conversationId, CancellationToken cancellationToken);

    // ----- Đồng bộ nhóm dự án -----
    Task<(Guid ProjectId, string Title)?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ProjectConversationMembers?> GetProjectConversationMembersAsync(Guid projectId, CancellationToken cancellationToken);

    // ----- Ghi -----
    // Lưu ý: entity có key set sẵn (Guid v7) phải được Add trực tiếp vào DbSet,
    // không thêm qua navigation của entity đã track (EF sẽ hiểu nhầm là Unchanged).
    Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken);
    Task AddParticipantAsync(ConversationParticipant participant, CancellationToken cancellationToken);
    Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken);
    Task AddReactionAsync(ChatMessageReaction reaction, CancellationToken cancellationToken);
    Task AddReportAsync(ChatReport report, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);

    // ----- Kiểm duyệt (Admin) -----
    Task<ChatReport?> GetReportAsync(Guid reportId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatReportAdminRow>> ListReportsAsync(string? status, int skip, int take, CancellationToken cancellationToken);
    Task<int> CountReportsAsync(string? status, CancellationToken cancellationToken);
    Task BanChatAsync(Guid targetUserId, string? reason, DateTimeOffset? until, CancellationToken cancellationToken);
    Task UnbanChatAsync(Guid targetUserId, CancellationToken cancellationToken);
    Task<int> CountChatBannedUsersAsync(CancellationToken cancellationToken);
    Task<int> CountAllConversationsAsync(CancellationToken cancellationToken);
    Task<int> CountAllMessagesAsync(CancellationToken cancellationToken);
}

/// <summary>Dòng báo cáo cho trang kiểm duyệt — Admin chỉ được xem nội dung tin nhắn qua đây.</summary>
public sealed record ChatReportAdminRow(
    Guid Id,
    Guid MessageId,
    Guid ReporterUserId,
    string ReporterDisplayName,
    string Reason,
    string Status,
    Guid? ResolvedByAdminUserId,
    string? ResolutionNote,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt,
    ChatMessageRow? Message);
