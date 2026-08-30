using System.Text.Json;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Notifications;

public sealed record NotificationResponse(Guid Id, string Type, string Title, string? PayloadJson, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
public sealed record NotificationPreferenceResponse(bool EmailEnabled, bool RealtimeEnabled);
public sealed record UpdateNotificationPreferenceRequest(bool EmailEnabled, bool RealtimeEnabled);

public sealed class NotificationWriter(AppDbContext dbContext) : INotificationWriter, IOutboxEnqueuer
{
    public void Add(NotificationMessage message)
    {
        var notification = new Notification(message.UserId, message.Type, message.Title, message.PayloadJson);
        dbContext.Notifications.Add(notification);
        Enqueue(NotificationOutboxEvent.TypeName, JsonSerializer.Serialize(new NotificationOutboxEvent(notification.Id, message.UserId)));
    }

    public void Enqueue(string type, string payloadJson) => dbContext.OutboxMessages.Add(new OutboxMessage(type, payloadJson));
}

public sealed record NotificationOutboxEvent(Guid NotificationId, Guid UserId)
{
    public const string TypeName = "notification.created";
}

public sealed class NotificationService(AppDbContext dbContext)
{
    public async Task<IReadOnlyCollection<NotificationResponse>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Notifications.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(MapExpression).ToListAsync(cancellationToken);

    public Task<NotificationResponse?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Notifications.AsNoTracking().Where(x => x.UserId == userId && x.Id == id).Select(MapExpression).SingleOrDefaultAsync(cancellationToken);

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken) => dbContext.Notifications.CountAsync(x => x.UserId == userId && x.ReadAt == null, cancellationToken);

    public async Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id, cancellationToken);
        if (notification is null) return false;
        notification.MarkRead(); await dbContext.SaveChangesAsync(cancellationToken); return true;
    }
    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var notifications = await dbContext.Notifications.Where(x => x.UserId == userId && x.ReadAt == null).ToListAsync(cancellationToken);
        foreach (var notification in notifications) notification.MarkRead();
        if (notifications.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id, cancellationToken);
        if (notification is null) return false;
        dbContext.Notifications.Remove(notification); await dbContext.SaveChangesAsync(cancellationToken); return true;
    }
    public async Task<NotificationPreferenceResponse> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var preference = await dbContext.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        return preference is null ? new NotificationPreferenceResponse(true, true) : new(preference.EmailEnabled, preference.RealtimeEnabled);
    }
    public async Task<NotificationPreferenceResponse> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferenceRequest request, CancellationToken cancellationToken)
    {
        var preference = await dbContext.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (preference is null) { preference = new NotificationPreference(userId); dbContext.NotificationPreferences.Add(preference); }
        preference.Update(request.EmailEnabled, request.RealtimeEnabled); await dbContext.SaveChangesAsync(cancellationToken); return new(preference.EmailEnabled, preference.RealtimeEnabled);
    }
    private static readonly System.Linq.Expressions.Expression<Func<Notification, NotificationResponse>> MapExpression = x => new NotificationResponse(x.Id, x.Type, x.Title, x.PayloadJson, x.CreatedAt, x.ReadAt);
}