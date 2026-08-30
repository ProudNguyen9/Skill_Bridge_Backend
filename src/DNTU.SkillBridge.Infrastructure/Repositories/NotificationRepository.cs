using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the notification data operations.</summary>
public sealed class NotificationRepository(AppDbContext dbContext) : INotificationRepository
{
    public async Task<IReadOnlyCollection<NotificationResponse>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Notifications.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(MapExpression).ToListAsync(cancellationToken);

    public Task<NotificationResponse?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Notifications.AsNoTracking().Where(x => x.UserId == userId && x.Id == id).Select(MapExpression).SingleOrDefaultAsync(cancellationToken);

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken) => dbContext.Notifications.CountAsync(x => x.UserId == userId && x.ReadAt == null, cancellationToken);

    public Task<Notification?> FindAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Notifications.AsTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id, cancellationToken);

    public Task<List<Notification>> FindUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Notifications.AsTracking().Where(x => x.UserId == userId && x.ReadAt == null).ToListAsync(cancellationToken);

    public void Remove(Notification notification) => dbContext.Notifications.Remove(notification);

    public Task<NotificationPreference?> FindPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<NotificationPreference?> FindPreferenceForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.NotificationPreferences.AsTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public void AddPreference(NotificationPreference preference) => dbContext.NotificationPreferences.Add(preference);

    private static readonly System.Linq.Expressions.Expression<Func<Notification, NotificationResponse>> MapExpression = x => new NotificationResponse(x.Id, x.Type, x.Title, x.PayloadJson, x.CreatedAt, x.ReadAt);
}
