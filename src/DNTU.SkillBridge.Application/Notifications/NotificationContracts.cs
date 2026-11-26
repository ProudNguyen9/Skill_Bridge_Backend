namespace DNTU.SkillBridge.Application.Notifications;

public sealed record NotificationMessage(Guid UserId, string Type, string Title, string? PayloadJson = null);
public interface INotificationWriter
{
    void Add(NotificationMessage message);

    /// <summary>
    /// Upsert thông báo chat chưa đọc theo hội thoại: nếu đã có thông báo CHAT_MESSAGE
    /// chưa đọc của cùng hội thoại thì cập nhật nội dung (tránh loạt thông báo cho mỗi tin),
    /// chưa có thì tạo mới. Cả hai trường hợp đều enqueue outbox để chuông realtime làm mới.
    /// </summary>
    Task UpsertUnreadChatNotificationAsync(Guid userId, string conversationId, string title, string payloadJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu đã đọc mọi thông báo CHAT_MESSAGE thuộc hội thoại khi người dùng đọc tin nhắn trong chat.
    /// </summary>
    Task MarkChatNotificationsReadAsync(Guid userId, string conversationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
public interface IOutboxEnqueuer { void Enqueue(string type, string payloadJson); }

/// <summary>Best-effort email for meeting reminders (skipped silently when Email:Enabled is false).</summary>
public interface IMeetingReminderEmailSender
{
    Task SendMeetingReminderAsync(IReadOnlyCollection<string> recipientEmails, string meetingTitle, DateTimeOffset startAtUtc, string? meetingUrl, CancellationToken cancellationToken);
}

public sealed record NotificationResponse(Guid Id, string Type, string Title, string? PayloadJson, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
public sealed record NotificationPreferenceResponse(bool EmailEnabled, bool RealtimeEnabled);
public sealed record UpdateNotificationPreferenceRequest(bool EmailEnabled, bool RealtimeEnabled);

public sealed record NotificationOutboxEvent(Guid NotificationId, Guid UserId)
{
    public const string TypeName = "notification.created";
}
