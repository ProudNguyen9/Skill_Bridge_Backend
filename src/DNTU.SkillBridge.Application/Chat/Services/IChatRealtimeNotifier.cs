using DNTU.SkillBridge.Domain.Chat;

namespace DNTU.SkillBridge.Application.Chat;

/// <summary>Broadcast realtime của ChatHub — hiện thực ở Api/Realtime bằng IHubContext.</summary>
public interface IChatRealtimeNotifier
{
    /// <summary>Gửi tin nhắn tới user-group của từng người nhận kèm số chưa đọc mới.</summary>
    Task NotifyMessageReceivedAsync(ChatMessageResponse message, IReadOnlyCollection<ChatMessageDelivery> deliveries, CancellationToken cancellationToken);

    Task NotifyMessageUpdatedAsync(ChatMessageResponse message, IReadOnlyCollection<Guid> participantUserIds, CancellationToken cancellationToken);

    Task NotifyMessageDeletedAsync(Guid conversationId, Guid messageId, IReadOnlyCollection<Guid> participantUserIds, CancellationToken cancellationToken);

    Task NotifyReactionToggledAsync(Guid conversationId, Guid messageId, string emoji, int count, IReadOnlyCollection<Guid> userEmojis, Guid changedByUserId, IReadOnlyCollection<Guid> participantUserIds, CancellationToken cancellationToken);

    Task NotifyTypingChangedAsync(Guid conversationId, Guid userId, bool isTyping, IReadOnlyCollection<Guid> otherUserIds, CancellationToken cancellationToken);

    Task NotifyConversationReadAsync(Guid conversationId, Guid userId, DateTimeOffset lastReadAt, IReadOnlyCollection<Guid> otherUserIds, CancellationToken cancellationToken);

    /// <summary>Hội thoại mới tạo (người nhận cần cập nhật danh sách hộp thư).</summary>
    Task NotifyConversationUpsertedAsync(Guid conversationId, IReadOnlyCollection<Guid> participantUserIds, CancellationToken cancellationToken);

    Task NotifyUnreadCountChangedAsync(Guid conversationId, Guid userId, int unreadCount, CancellationToken cancellationToken);
}

/// <summary>Theo dõi trạng thái Online in-memory (single-node, sẵn sàng mở rộng Redis backplane).</summary>
public interface IChatPresenceTracker
{
    /// <returns>true nếu người dùng vừa chuyển từ Offline sang Online.</returns>
    bool Connect(Guid userId, string connectionId);

    /// <returns>true nếu người dùng vừa chuyển từ Online sang Offline.</returns>
    bool Disconnect(Guid userId, string connectionId);

    bool IsOnline(Guid userId);

    IReadOnlyCollection<Guid> GetOnlineUserIds(IReadOnlyCollection<Guid> userIds);

    IReadOnlyCollection<Guid> GetAllOnlineUserIds();
}
