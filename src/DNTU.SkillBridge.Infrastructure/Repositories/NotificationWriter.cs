using System.Text.Json;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

public sealed class NotificationWriter(AppDbContext dbContext) : INotificationWriter, IOutboxEnqueuer
{
    public const string ChatMessageNotificationType = "CHAT_MESSAGE";

    public void Add(NotificationMessage message)
    {
        var notification = new Notification(message.UserId, message.Type, message.Title, message.PayloadJson);
        dbContext.Notifications.Add(notification);
        Enqueue(NotificationOutboxEvent.TypeName, JsonSerializer.Serialize(new NotificationOutboxEvent(notification.Id, message.UserId)));
    }

    /// <inheritdoc />
    public async Task UpsertUnreadChatNotificationAsync(Guid userId, string conversationId, string title, string payloadJson, CancellationToken cancellationToken = default)
    {
        // Thông báo CHAT_MESSAGE chưa đọc của CÙNG hội thoại → cập nhật thay vì tạo mới (chống loạt chuông).
        var existing = await dbContext.Notifications
            .Where(n => n.UserId == userId
                && n.Type == ChatMessageNotificationType
                && n.ReadAt == null
                && n.PayloadJson != null
                && n.PayloadJson.Contains(conversationId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            existing.Refresh(title, payloadJson);
            Enqueue(NotificationOutboxEvent.TypeName, JsonSerializer.Serialize(new NotificationOutboxEvent(existing.Id, userId)));
            return;
        }

        var notification = new Notification(userId, ChatMessageNotificationType, title, payloadJson);
        dbContext.Notifications.Add(notification);
        Enqueue(NotificationOutboxEvent.TypeName, JsonSerializer.Serialize(new NotificationOutboxEvent(notification.Id, userId)));
    }

    public void Enqueue(string type, string payloadJson) => dbContext.OutboxMessages.Add(new OutboxMessage(type, payloadJson));
}
