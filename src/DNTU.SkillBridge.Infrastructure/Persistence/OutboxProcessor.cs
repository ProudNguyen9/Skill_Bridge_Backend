using System.Text.Json;
using DNTU.SkillBridge.Application.Meetings;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Meetings;
using DNTU.SkillBridge.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DNTU.SkillBridge.Infrastructure.Persistence;

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
        var notificationWriter = scope.ServiceProvider.GetRequiredService<INotificationWriter>();
        var meetingEmailSender = scope.ServiceProvider.GetRequiredService<IMeetingReminderEmailSender>();
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
                else if (message.Type == "meeting.reminder.schedule")
                {
                    await DispatchMeetingReminderAsync(message, dbContext, notificationWriter, meetingEmailSender, logger, cancellationToken);
                }
                message.MarkProcessed();
            }
            catch (Exception exception) { message.Retry("Dispatch failed."); logger.LogWarning(exception, "Outbox message {MessageId} dispatch failed.", message.Id); }
        }
        if (messages.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Fires the 30-minute-before-start reminder for one meeting. A stale job (meeting rescheduled or
    /// cancelled since the job was written) is skipped: the payload's StartAt must still match the live
    /// meeting and the reminder state must be SCHEDULED.
    /// </summary>
    private static async Task DispatchMeetingReminderAsync(
        Domain.Notifications.OutboxMessage message,
        AppDbContext dbContext,
        INotificationWriter notificationWriter,
        IMeetingReminderEmailSender meetingEmailSender,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var job = JsonSerializer.Deserialize<MeetingReminderJob>(message.PayloadJson) ?? throw new InvalidOperationException("Invalid meeting reminder outbox payload.");
        var meeting = await dbContext.ProjectMeetings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == job.MeetingId, cancellationToken);
        if (meeting is null || meeting.ReminderState != MeetingReminderState.SCHEDULED || meeting.StartAt != job.StartAt || meeting.StartAt <= DateTimeOffset.UtcNow)
        {
            return;
        }

        var recipients = await dbContext.MeetingParticipants.AsNoTracking()
            .Where(item => item.MeetingId == meeting.Id)
            .Join(dbContext.StudentProfiles, participant => participant.StudentId, student => student.Id, (participant, student) => new { student.UserId })
            .Join(dbContext.Users, row => row.UserId, user => user.Id, (row, user) => new { row.UserId, user.Email })
            .ToListAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            meetingId = meeting.Id,
            projectId = meeting.ProjectId,
            meetingType = meeting.MeetingType.ToString(),
            startAt = meeting.StartAt,
            meetingUrl = meeting.ExternalUrl
        });
        var title = $"Sắp có cuộc họp: {meeting.Title}";
        foreach (var recipient in recipients)
        {
            notificationWriter.Add(new NotificationMessage(recipient.UserId, "MEETING_REMINDER", title, payload));
        }

        // Best-effort email — a failure must not fail the whole outbox batch.
        try
        {
            var emails = recipients.Select(item => item.Email).Where(email => !string.IsNullOrWhiteSpace(email)).ToArray().AsReadOnly();
            await meetingEmailSender.SendMeetingReminderAsync(emails, meeting.Title, meeting.StartAt, meeting.ExternalUrl, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Meeting reminder email for meeting {MeetingId} failed.", meeting.Id);
        }
    }
}
