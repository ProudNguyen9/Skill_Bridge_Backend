using DNTU.SkillBridge.Application.Chat;
using DNTU.SkillBridge.Application.Chat.Repositories;
using DNTU.SkillBridge.Domain.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DNTU.SkillBridge.Api.Realtime;

/// <summary>
/// Hub chat realtime: presence, join/leave phòng, typing indicator, đánh dấu đã đọc.
/// Tất cả sự kiện dữ liệu broadcast qua user-group của từng thành viên để dock/inbox đồng bộ đa thiết bị.
/// </summary>
[Authorize]
public sealed class ChatHub(IChatRepository chatRepository, IChatPresenceTracker presenceTracker) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId is null)
        {
            throw new HubException("Chat access is denied.");
        }

        var cameOnline = presenceTracker.Connect(userId.Value, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value), Context.ConnectionAborted);

        if (cameOnline)
        {
            await Clients.All.SendAsync("PresenceChanged", new { userId = userId.Value, isOnline = true }, Context.ConnectionAborted);
        }

        await Clients.Caller.SendAsync("PresenceSnapshot", new { onlineUserIds = presenceTracker.GetAllOnlineUserIds() }, Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId is not null)
        {
            var wentOffline = presenceTracker.Disconnect(userId.Value, Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId.Value));
            if (wentOffline)
            {
                await Clients.All.SendAsync("PresenceChanged", new { userId = userId.Value, isOnline = false });
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Tham gia phòng hội thoại (kiểm tra thành viên đã chấp nhận).</summary>
    public async Task JoinConversation(Guid conversationId)
    {
        var userId = RequireUserId();
        if (!await chatRepository.IsAcceptedParticipantAsync(conversationId, userId, Context.ConnectionAborted))
        {
            throw new HubException("Conversation access is denied.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId), Context.ConnectionAborted);
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ConversationGroup(conversationId), Context.ConnectionAborted);
    }

    /// <summary>Typing indicator — chỉ broadcast tới các thành viên khác trong phòng.</summary>
    public async Task SendTyping(Guid conversationId, bool isTyping)
    {
        var userId = RequireUserId();
        if (!await chatRepository.IsAcceptedParticipantAsync(conversationId, userId, Context.ConnectionAborted))
        {
            throw new HubException("Conversation access is denied.");
        }

        var otherUserIds = await ListOtherParticipantIdsAsync(conversationId, userId);
        await Clients.Groups(otherUserIds.Select(UserGroup).ToList())
            .SendAsync("TypingChanged", new { conversationId, userId, isTyping }, Context.ConnectionAborted);
    }

    public static string UserGroup(Guid userId) => $"user:{userId:N}";

    public static string ConversationGroup(Guid conversationId) => $"conversation:{conversationId:N}";

    private Guid RequireUserId() => GetUserId() ?? throw new HubException("Chat access is denied.");

    private Guid? GetUserId()
    {
        var claim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim is not null && Guid.TryParse(claim.Value, out var userId) ? userId : null;
    }

    private async Task<IReadOnlyList<Guid>> ListOtherParticipantIdsAsync(Guid conversationId, Guid exceptUserId)
    {
        var statuses = await chatRepository.GetParticipantStatusesAsync(conversationId, Context.ConnectionAborted);
        return statuses
            .Where(p => p.UserId != exceptUserId && p.LeftAt is null && p.Status != ConversationRequestStatus.DECLINED)
            .Select(p => p.UserId)
            .ToList();
    }
}
