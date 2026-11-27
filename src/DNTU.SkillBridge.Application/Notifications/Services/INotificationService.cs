using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Notifications;

namespace DNTU.SkillBridge.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyCollection<NotificationResponse>> ListAsync(
        Guid userId,
        int page = 1,
        int pageSize = 30,
        bool excludeChatMessages = false,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<int> UnreadCountAsync(Guid userId, bool excludeChatMessages = false, CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task MarkAllReadAsync(Guid userId, bool excludeChatMessages = false, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<NotificationPreferenceResponse> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    Task<NotificationPreferenceResponse> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferenceRequest request, CancellationToken cancellationToken);
}
