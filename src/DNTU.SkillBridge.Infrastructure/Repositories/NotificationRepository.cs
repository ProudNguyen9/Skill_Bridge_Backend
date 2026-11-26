using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the notification data operations.</summary>
public sealed class NotificationRepository(AppDbContext dbContext) : INotificationRepository
{
    public async Task<IReadOnlyCollection<NotificationResponse>> ListAsync(
        Guid userId,
        int page = 1,
        int pageSize = 30,
        bool excludeChatMessages = false,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Notifications.AsNoTracking().Where(x => x.UserId == userId);

        if (excludeChatMessages)
        {
            query = query.Where(x => x.Type != NotificationWriter.ChatMessageNotificationType);
        }

        if (unreadOnly)
        {
            query = query.Where(x => x.ReadAt == null);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .Select(MapExpression)
            .ToListAsync(cancellationToken);
    }

    public Task<NotificationResponse?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Notifications.AsNoTracking().Where(x => x.UserId == userId && x.Id == id).Select(MapExpression).SingleOrDefaultAsync(cancellationToken);

    public Task<int> UnreadCountAsync(Guid userId, bool excludeChatMessages = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Notifications.Where(x => x.UserId == userId && x.ReadAt == null);
        if (excludeChatMessages)
        {
            query = query.Where(x => x.Type != NotificationWriter.ChatMessageNotificationType);
        }
        return query.CountAsync(cancellationToken);
    }

    public Task<Notification?> FindAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Notifications.AsTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id, cancellationToken);

    public Task<List<Notification>> FindUnreadAsync(Guid userId, bool excludeChatMessages = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Notifications.AsTracking().Where(x => x.UserId == userId && x.ReadAt == null);
        if (excludeChatMessages)
        {
            query = query.Where(x => x.Type != NotificationWriter.ChatMessageNotificationType);
        }
        return query.ToListAsync(cancellationToken);
    }

    public void Remove(Notification notification) => dbContext.Notifications.Remove(notification);

    public Task<NotificationPreference?> FindPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<NotificationPreference?> FindPreferenceForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.NotificationPreferences.AsTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public void AddPreference(NotificationPreference preference) => dbContext.NotificationPreferences.Add(preference);

    private static readonly System.Linq.Expressions.Expression<Func<Notification, NotificationResponse>> MapExpression = x => new NotificationResponse(x.Id, x.Type, x.Title, x.PayloadJson, x.CreatedAt, x.ReadAt);
}
