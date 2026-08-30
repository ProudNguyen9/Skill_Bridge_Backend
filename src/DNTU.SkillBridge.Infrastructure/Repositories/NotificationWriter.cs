using System.Text.Json;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Infrastructure.Persistence;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

public sealed class NotificationWriter(AppDbContext dbContext) : INotificationWriter, IOutboxEnqueuer
{
    public void Add(NotificationMessage message)
    {
        var notification = new Notification(message.UserId, message.Type, message.Title, message.PayloadJson);
        dbContext.Notifications.Add(notification);
        Enqueue(NotificationOutboxEvent.TypeName, JsonSerializer.Serialize(new NotificationOutboxEvent(notification.Id, message.UserId)));
    }

    public void Enqueue(string type, string payloadJson) => dbContext.OutboxMessages.Add(new OutboxMessage(type, payloadJson));
}
