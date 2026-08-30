using DNTU.SkillBridge.Domain.Notifications;

namespace DNTU.SkillBridge.Application.Notifications;

/// <summary>Data operations for user notifications and notification preferences.</summary>
public interface INotificationRepository
{
    Task<IReadOnlyCollection<NotificationResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<NotificationResponse?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Reads a user notification as a tracked entity so mutations are persisted.</summary>
    Task<Notification?> FindAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    /// <summary>Reads all unread notifications of a user as tracked entities so mutations are persisted.</summary>
    Task<List<Notification>> FindUnreadAsync(Guid userId, CancellationToken cancellationToken);

    void Remove(Notification notification);

    Task<NotificationPreference?> FindPreferenceAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Reads a notification preference as a tracked entity so mutations are persisted.</summary>
    Task<NotificationPreference?> FindPreferenceForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    void AddPreference(NotificationPreference preference);
}
