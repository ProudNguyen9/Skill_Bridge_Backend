using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Notifications;

namespace DNTU.SkillBridge.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyCollection<NotificationResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<NotificationResponse?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<NotificationPreferenceResponse> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    Task<NotificationPreferenceResponse> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferenceRequest request, CancellationToken cancellationToken);
}

public sealed class NotificationService(INotificationRepository notificationRepository, IUnitOfWork unitOfWork) : INotificationService
{
    public async Task<IReadOnlyCollection<NotificationResponse>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        await notificationRepository.ListAsync(userId, cancellationToken);

    public Task<NotificationResponse?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        notificationRepository.GetAsync(userId, id, cancellationToken);

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken) => notificationRepository.UnreadCountAsync(userId, cancellationToken);

    public async Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var notification = await notificationRepository.FindAsync(userId, id, cancellationToken);
        if (notification is null) return false;
        notification.MarkRead(); await unitOfWork.SaveChangesAsync(cancellationToken); return true;
    }
    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var notifications = await notificationRepository.FindUnreadAsync(userId, cancellationToken);
        foreach (var notification in notifications) notification.MarkRead();
        if (notifications.Count > 0) await unitOfWork.SaveChangesAsync(cancellationToken);
    }
    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var notification = await notificationRepository.FindAsync(userId, id, cancellationToken);
        if (notification is null) return false;
        notificationRepository.Remove(notification); await unitOfWork.SaveChangesAsync(cancellationToken); return true;
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
