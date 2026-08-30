using System.Text.Json;
using DNTU.SkillBridge.Application.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using DNTU.SkillBridge.Infrastructure.Persistence;

namespace DNTU.SkillBridge.Api.Realtime;

public sealed class OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchAvailableAsync(stoppingToken); }
            catch (Exception exception) { logger.LogError(exception, "Outbox dispatch cycle failed."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
    private async Task DispatchAvailableAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();
        var messages = await dbContext.OutboxMessages.Where(x => x.ProcessedAt == null && x.AvailableAt <= DateTimeOffset.UtcNow).OrderBy(x => x.CreatedAt).Take(50).ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            try
            {
                if (message.Type == NotificationOutboxEvent.TypeName)
                {
                    var notification = JsonSerializer.Deserialize<NotificationOutboxEvent>(message.PayloadJson) ?? throw new InvalidOperationException("Invalid notification outbox payload.");
                    await hub.Clients.User(notification.UserId.ToString()).SendAsync("notifications.changed", new { notificationId = notification.NotificationId, change = "created" }, cancellationToken);
                }
                message.MarkProcessed();
            }
            catch (Exception exception) { message.Retry("Dispatch failed."); logger.LogWarning(exception, "Outbox message {MessageId} dispatch failed.", message.Id); }
        }
        if (messages.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
    }
}