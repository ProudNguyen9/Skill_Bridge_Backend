namespace DNTU.SkillBridge.Application.Notifications;

public sealed record NotificationMessage(Guid UserId, string Type, string Title, string? PayloadJson = null);
public interface INotificationWriter { void Add(NotificationMessage message); }
public interface IOutboxEnqueuer { void Enqueue(string type, string payloadJson); }