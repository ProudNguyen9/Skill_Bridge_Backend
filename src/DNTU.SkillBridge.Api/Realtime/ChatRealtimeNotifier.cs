using DNTU.SkillBridge.Application.Chat;
using Microsoft.AspNetCore.SignalR;

namespace DNTU.SkillBridge.Api.Realtime;

/// <summary>Broadcast chat qua IHubContext&lt;ChatHub&gt; — gọi sau khi service lưu DB thành công.</summary>
public sealed class ChatRealtimeNotifier(IHubContext<ChatHub> hubContext) : IChatRealtimeNotifier
{
    public async Task NotifyMessageReceivedAsync(ChatMessageResponse message, IReadOnlyCollection<ChatMessageDelivery> deliveries, CancellationToken cancellationToken)
    {
        foreach (var delivery in deliveries)
        {
            await hubContext.Clients
                .Group(ChatHub.UserGroup(delivery.UserId))
                .SendAsync("MessageReceived", new { message, unreadCount = delivery.UnreadCount }, cancellationToken);
        }
    }

    public async Task NotifyMessageUpdatedAsync(ChatMessageResponse message, IReadOnlyCollection<Guid> participantUserIds, CancellationToken cancellationToken)
    {
        foreach (var userId in participantUserIds)
        {
            await hubContext.Clients
                .Group(ChatHub.UserGroup(userId))
                .SendAsync("MessageUpdated", message, cancellationToken);
        }
    }

    public async Task NotifyMessageDeletedAsync(Guid conversationId, Guid messageId, IReadOnlyCollection<Guid> participantUserIds, CancellationToken cancellationToken)
    {
        foreach (var userId in participantUserIds)
        {
            await hubContext.Clients
                .Group(ChatHub.UserGroup(userId))
                .SendAsync("MessageDeleted", new { conversationId, messageId }, cancellationToken);
        }
    }

    public async Task NotifyReactionToggledAsync(Guid conversationId, Guid messageId, string emoji, int count, IReadOnlyCollection<Guid> userEmojis, Guid changedByUserId, IReadOnlyCollection<Guid> participantUserIds, CancellationToken cancellationToken)
    {
        var payload = new { conversationId, messageId, emoji, count, userIds = userEmojis, changedByUserId };
        foreach (var userId in participantUserIds)
        {
            await hubContext.Clients
                .Group(ChatHub.UserGroup(userId))
                .SendAsync("ReactionToggled", payload, cancellationToken);
        }
    }

    public async Task NotifyTypingChangedAsync(Guid conversationId, Guid userId, bool isTyping, IReadOnlyCollection<Guid> otherUserIds, CancellationToken cancellationToken)
    {
        foreach (var otherUserId in otherUserIds)
        {
            await hubContext.Clients
                .Group(ChatHub.UserGroup(otherUserId))
                .SendAsync("TypingChanged", new { conversationId, userId, isTyping }, cancellationToken);
        }
    }

    public async Task NotifyConversationReadAsync(Guid conversationId, Guid userId, DateTimeOffset lastReadAt, IReadOnlyCollection<Guid> otherUserIds, CancellationToken cancellationToken)
    {
        var payload = new { conversationId, userId, lastReadAt };
        foreach (var otherUserId in otherUserIds)
        {
            await hubContext.Clients
                .Group(ChatHub.UserGroup(otherUserId))
                .SendAsync("ConversationRead", payload, cancellationToken);
        }
    }

    public async Task NotifyConversationUpsertedAsync(Guid conversationId, IReadOnlyCollection<Guid> participantUserIds, CancellationToken cancellationToken)
    {
        foreach (var userId in participantUserIds)
        {
            await hubContext.Clients
                .Group(ChatHub.UserGroup(userId))
                .SendAsync("ConversationUpserted", new { conversationId }, cancellationToken);
        }
    }

    public async Task NotifyUnreadCountChangedAsync(Guid conversationId, Guid userId, int unreadCount, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(ChatHub.UserGroup(userId))
            .SendAsync("UnreadCountChanged", new { conversationId, unreadCount }, cancellationToken);
    }
}
