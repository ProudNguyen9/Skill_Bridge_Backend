using System.Text.Json;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Notifications;

namespace DNTU.SkillBridge.Application.Notifications;

public sealed class NotificationService(
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork,
    IOutboxEnqueuer? outboxEnqueuer = null) : INotificationService
{
    public async Task<IReadOnlyCollection<NotificationResponse>> ListAsync(
        Guid userId,
        int page = 1,
        int pageSize = 30,
        bool excludeChatMessages = false,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default) =>
        await notificationRepository.ListAsync(userId, page, pageSize, excludeChatMessages, unreadOnly, cancellationToken);

    public Task<NotificationResponse?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        notificationRepository.GetAsync(userId, id, cancellationToken);

    public Task<int> UnreadCountAsync(Guid userId, bool excludeChatMessages = false, CancellationToken cancellationToken = default) =>
        notificationRepository.UnreadCountAsync(userId, excludeChatMessages, cancellationToken);

    public async Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var notification = await notificationRepository.FindAsync(userId, id, cancellationToken);
        if (notification is null) return false;
        if (notification.ReadAt is null)
        {
            notification.MarkRead();
            outboxEnqueuer?.Enqueue(NotificationOutboxEvent.TypeName, JsonSerializer.Serialize(new NotificationOutboxEvent(id, userId)));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public async Task MarkAllReadAsync(Guid userId, bool excludeChatMessages = false, CancellationToken cancellationToken = default)
    {
        var notifications = await notificationRepository.FindUnreadAsync(userId, excludeChatMessages, cancellationToken);
        foreach (var notification in notifications) notification.MarkRead();
        if (notifications.Count > 0)
        {
            outboxEnqueuer?.Enqueue(NotificationOutboxEvent.TypeName, JsonSerializer.Serialize(new NotificationOutboxEvent(notifications[0].Id, userId)));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var notification = await notificationRepository.FindAsync(userId, id, cancellationToken);
        if (notification is null) return false;
        notificationRepository.Remove(notification);
        outboxEnqueuer?.Enqueue(NotificationOutboxEvent.TypeName, JsonSerializer.Serialize(new NotificationOutboxEvent(id, userId)));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
    public async Task<NotificationPreferenceResponse> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var preference = await notificationRepository.FindPreferenceAsync(userId, cancellationToken);
        return preference is null ? new NotificationPreferenceResponse(true, true) : new(preference.EmailEnabled, preference.RealtimeEnabled);
    }
    public async Task<NotificationPreferenceResponse> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferenceRequest request, CancellationToken cancellationToken)
    {
        var preference = await notificationRepository.FindPreferenceForUpdateAsync(userId, cancellationToken);
        if (preference is null) { preference = new NotificationPreference(userId); notificationRepository.AddPreference(preference); }
        preference.Update(request.EmailEnabled, request.RealtimeEnabled); await unitOfWork.SaveChangesAsync(cancellationToken); return new(preference.EmailEnabled, preference.RealtimeEnabled);
    }
}
