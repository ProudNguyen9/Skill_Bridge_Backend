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
