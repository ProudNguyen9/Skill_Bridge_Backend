using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Notifications;

public sealed class Notification : AuditableEntity
{
    public Guid UserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? PayloadJson { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    private Notification() { }

    public Notification(Guid userId, string type, string title, string? payloadJson = null)
    {
        UserId = userId;
        Type = Required(type, nameof(type), 100);
        Title = Required(title, nameof(title), 300);
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? null : payloadJson.Trim();
    }

    public void MarkRead() => ReadAt ??= DateTimeOffset.UtcNow;
    private static string Required(string value, string name, int maxLength) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength ? value.Trim() : throw new ArgumentException($"{name} is required and must be at most {maxLength} characters.", name);
}

public sealed class NotificationPreference : AuditableEntity
{
    public Guid UserId { get; private set; }
    public bool EmailEnabled { get; private set; } = true;
    public bool RealtimeEnabled { get; private set; } = true;

    private NotificationPreference() { }
    public NotificationPreference(Guid userId) => UserId = userId;
    public void Update(bool emailEnabled, bool realtimeEnabled) => (EmailEnabled, RealtimeEnabled) = (emailEnabled, realtimeEnabled);
}

public sealed class OutboxMessage : AuditableEntity
{
    public string Type { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTimeOffset AvailableAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage() { }
    public OutboxMessage(string type, string payloadJson)
    {
        Type = string.IsNullOrWhiteSpace(type) ? throw new ArgumentException("An outbox type is required.", nameof(type)) : type.Trim();
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? throw new ArgumentException("An outbox payload is required.", nameof(payloadJson)) : payloadJson;
        AvailableAt = DateTimeOffset.UtcNow;
    }
    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;
    public void Retry(string safeError)
    {
        RetryCount++;
        LastError = safeError.Length > 500 ? safeError[..500] : safeError;
        AvailableAt = DateTimeOffset.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, RetryCount)));
    }
}