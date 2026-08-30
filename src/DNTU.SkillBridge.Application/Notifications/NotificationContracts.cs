namespace DNTU.SkillBridge.Application.Notifications;

public sealed record NotificationMessage(Guid UserId, string Type, string Title, string? PayloadJson = null);
public interface INotificationWriter { void Add(NotificationMessage message); }
public interface IOutboxEnqueuer { void Enqueue(string type, string payloadJson); }

public sealed record NotificationResponse(Guid Id, string Type, string Title, string? PayloadJson, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
public sealed record NotificationPreferenceResponse(bool EmailEnabled, bool RealtimeEnabled);
public sealed record UpdateNotificationPreferenceRequest(bool EmailEnabled, bool RealtimeEnabled);

public sealed record NotificationOutboxEvent(Guid NotificationId, Guid UserId)
{
    public const string TypeName = "notification.created";
}
