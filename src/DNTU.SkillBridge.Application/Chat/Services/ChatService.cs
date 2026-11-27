using System.Text.Json;
using DNTU.SkillBridge.Application.Chat.Repositories;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Files;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Chat;
using DNTU.SkillBridge.Domain.Social;

namespace DNTU.SkillBridge.Application.Chat.Services;

/// <summary>
/// Nghiệp vụ chat realtime: hội thoại 1-1 + nhóm dự án, tin nhắn chờ, chặn/báo cáo,
/// đính kèm presigned URL, reaction, đã xem. Rate limit gửi tin do endpoint áp dụng.
/// </summary>
public sealed class ChatService(
    IChatRepository chatRepository,
    IChatRealtimeNotifier realtimeNotifier,
    IChatPresenceTracker presenceTracker,
    IFileStorage fileStorage,
    INotificationWriter? notificationWriter = null) : IChatService
{
    public const long MaxAttachmentBytes = 15 * 1024 * 1024;
    public const int MaxNewMessageRequestsPerDay = 10;
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly string[] DocumentExtensions = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".txt", ".csv"];

    public async Task<(ChatOutcome, ChatConversationResponse?, string?)> OpenDirectConversationAsync(Guid userId, OpenDirectConversationRequest request, CancellationToken cancellationToken)
    {
        var targetUserId = request.TargetUserId;
        if (targetUserId == userId)
        {
            return (ChatOutcome.InvalidOperation, null, "Không thể tự nhắn tin cho chính mình.");
        }

        var target = await chatRepository.GetUserChatStateAsync(targetUserId, cancellationToken);
        if (target is null || target.Value.UserId != targetUserId)
        {
            return (ChatOutcome.TargetNotFound, null, "Không tìm thấy người dùng cần nhắn tin.");
        }

        if (await IsAdminUserAsync(targetUserId, cancellationToken))
        {
            return (ChatOutcome.TargetIsAdmin, null, "Không thể nhắn tin với tài khoản quản trị viên.");
        }

        if (await chatRepository.IsBlockedBetweenAsync(userId, targetUserId, cancellationToken))
        {
            return (ChatOutcome.BlockedByParticipant, null, "Hội thoại với người dùng này hiện không khả dụng.");
        }

        var requesterState = await chatRepository.GetUserChatStateAsync(userId, cancellationToken);
        if (requesterState.HasValue && requesterState.Value.ChatBanReason is not null
            && (requesterState.Value.ChatBannedUntil is null || requesterState.Value.ChatBannedUntil.Value > DateTimeOffset.UtcNow))
        {
            return (ChatOutcome.ChatBanned, null, $"Quyền nhắn tin của bạn đang bị tạm khóa. {requesterState.Value.ChatBanReason}".Trim());
        }

        var conversation = await chatRepository.GetDirectConversationAsync(userId, targetUserId, cancellationToken);
        var isNew = conversation is null;
        if (isNew)
        {
            var hasRelationship = await chatRepository.HasDirectRelationshipAsync(userId, targetUserId, cancellationToken);
            if (!hasRelationship)
            {
                var dayStartUtc = DateTimeOffset.UtcNow.Date;
                var createdToday = await chatRepository.CountNewPendingRequestsCreatedTodayAsync(userId, dayStartUtc, cancellationToken);
                if (createdToday >= MaxNewMessageRequestsPerDay)
                {
                    return (ChatOutcome.RateLimited, null, $"Bạn chỉ có thể tạo tối đa {MaxNewMessageRequestsPerDay} tin nhắn chờ mới trong một ngày.");
                }
            }

            conversation = Domain.Chat.Conversation.CreateDirect(userId, targetUserId, userId, hasRelationship ? ConversationRequestStatus.ACCEPTED : ConversationRequestStatus.PENDING_REQUEST);

            await chatRepository.AddConversationAsync(conversation, cancellationToken);
            // Lưu hội thoại TRƯỚC để tránh circular dependency khi insert cùng lúc
            // Conversation.LastMessageId -> ChatMessage.ConversationId.
            await chatRepository.SaveAsync(cancellationToken);
        }

        ChatMessageResponse? contextCardMessage = null;
        if (request.ContextCard is not null && isNew)
        {
            var card = request.ContextCard;
            var metadata = JsonSerializer.Serialize(new
            {
                cardType = card.CardType,
                entityId = card.EntityId,
                title = card.Title,
                subtitle = card.Subtitle,
                linkUrl = card.LinkUrl,
                extra = card.Extra,
            });
            var cardContent = $"Đã chia sẻ thẻ ngữ cảnh: {card.Title}";
            var message = new ChatMessage(conversation.Id, userId, ChatMessageType.CONTEXT_CARD, cardContent, metadata);
            await chatRepository.AddMessageAsync(message, cancellationToken);
            conversation.TouchLastMessage(message.Id, message.BuildPreview(), DateTimeOffset.UtcNow);
            await chatRepository.SaveAsync(cancellationToken);
            contextCardMessage = await MapMessageAsync(message.Id, cancellationToken);
        }

        await BroadcastConversationUpsertAsync(conversation, cancellationToken);

        if (isNew)
        {
            var targetStatus = conversation.Participants.Single(p => p.UserId == targetUserId).RequestStatus;
            if (targetStatus == ConversationRequestStatus.PENDING_REQUEST)
            {
                var requesterName = (await chatRepository.GetUserDisplayAsync(userId, cancellationToken))?.DisplayName ?? "Ai đó";
                notificationWriter?.Add(new NotificationMessage(
                    targetUserId,
                    "CHAT_REQUEST",
                    $"{requesterName} muốn nhắn tin với bạn",
                    JsonSerializer.Serialize(new { conversationId = conversation.Id, userId })));
                await chatRepository.SaveAsync(cancellationToken);
            }
        }

        var row = await chatRepository.GetConversationRowAsync(conversation.Id, userId, cancellationToken);
        var response = row is null ? null : MapConversationRow(row, presenceTracker);
        return (ChatOutcome.Success, response, null);
    }

    public async Task<(ChatOutcome, ChatConversationResponse?, string?)> OpenCompanyConversationAsync(Guid userId, Guid companyId, ContextCardPayload? contextCard, CancellationToken cancellationToken)
    {
        var companyUser = await chatRepository.GetCompanyPrimaryUserAsync(companyId, cancellationToken);
        if (companyUser is null)
        {
            return (ChatOutcome.TargetNotFound, null, "Không tìm thấy doanh nghiệp này.");
        }

        return await OpenDirectConversationAsync(userId, new OpenDirectConversationRequest(companyUser.Value, contextCard), cancellationToken);
    }

    public async Task<(ChatOutcome, ChatConversationResponse?, string?)> OpenProjectConversationAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await chatRepository.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
        {
            return (ChatOutcome.ProjectNotFound, null, "Không tìm thấy dự án.");
        }

        var hasAccess = await chatRepository.HasProjectAccessAsync(userId, projectId, cancellationToken);
        if (!hasAccess)
        {
            return (ChatOutcome.Forbidden, null, "Bạn không có quyền truy cập phòng chat của dự án này.");
        }

        var conversation = await chatRepository.GetProjectConversationAsync(projectId, cancellationToken);
        var isNew = conversation is null;
        if (conversation is null)
        {
            conversation = Domain.Chat.Conversation.CreateProjectGroup(projectId, project.Value.Title, userId);
        }

        var membership = await chatRepository.GetProjectConversationMembersAsync(projectId, cancellationToken)
            ?? new ProjectConversationMembers([], [], []);

        await SyncProjectParticipantsAsync(conversation, membership, cancellationToken);

        if (isNew)
        {
            await chatRepository.AddConversationAsync(conversation, cancellationToken);
        }

        await chatRepository.SaveAsync(cancellationToken);
        await BroadcastConversationUpsertAsync(conversation, cancellationToken);

        var row = await chatRepository.GetConversationRowAsync(conversation.Id, userId, cancellationToken);
        return (ChatOutcome.Success, row is null ? null : MapConversationRow(row, presenceTracker), null);
    }

    public async Task<IReadOnlyList<ChatConversationResponse>> GetConversationsAsync(Guid userId, string? tab, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var inboxTab = ParseTab(tab);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var rows = await chatRepository.ListConversationsAsync(userId, inboxTab, search, (page - 1) * pageSize, pageSize, cancellationToken);
        return rows.Select(row => MapConversationRow(row, presenceTracker)).ToList();
    }

    public async Task<ChatConversationDetailResponse?> GetConversationDetailAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        if (!await chatRepository.IsParticipantAsync(conversationId, userId, cancellationToken))
        {
            return null;
        }

        var row = await chatRepository.GetConversationRowAsync(conversationId, userId, cancellationToken);
        if (row is null) return null;

        var participants = await chatRepository.GetConversationParticipantsAsync(conversationId, cancellationToken);
        var userSummaries = participants
            .Where(p => p.LeftAt is null)
            .Select(p => new ChatUserSummary(
                p.UserId,
                p.DisplayName,
                p.AvatarUrl,
                p.Role,
                presenceTracker.IsOnline(p.UserId),
                null))
            .ToList();
        var readStates = (await chatRepository.GetParticipantReadStatesAsync(conversationId, cancellationToken))
            .Select(r => new ParticipantReadState(r.UserId, r.LastReadMessageId, r.LastReadAt))
            .ToList();

        return new ChatConversationDetailResponse(
            row.Id,
            row.Type.ToString(),
            row.Title,
            row.ProjectId,
            row.ProjectId is null ? null : new ChatProjectSummary(row.ProjectId.Value, row.ProjectTitle ?? string.Empty),
            userSummaries,
            readStates,
            userSummaries.Count(p => p.IsOnline));
    }

    public Task<int> GetTotalUnreadCountAsync(Guid userId, CancellationToken cancellationToken) =>
        chatRepository.GetTotalUnreadCountAsync(userId, cancellationToken);

    public async Task SetPinnedAsync(Guid userId, Guid conversationId, bool pinned, CancellationToken cancellationToken)
    {
        var participant = await GetAcceptedParticipantAsync(conversationId, userId, cancellationToken)
            ?? throw new InvalidOperationException("Bạn không phải thành viên của hội thoại này.");
        participant.SetPinned(pinned);
        await chatRepository.SaveAsync(cancellationToken);
    }

    public async Task SetMutedAsync(Guid userId, Guid conversationId, bool muted, CancellationToken cancellationToken)
    {
        var participant = await GetAcceptedParticipantAsync(conversationId, userId, cancellationToken)
            ?? throw new InvalidOperationException("Bạn không phải thành viên của hội thoại này.");
        participant.SetMuted(muted);
        await chatRepository.SaveAsync(cancellationToken);
    }

    public async Task<ChatMessagesPageResponse?> GetMessagesAsync(Guid userId, Guid conversationId, Guid? beforeMessageId, int limit, CancellationToken cancellationToken)
    {
        if (!await chatRepository.IsParticipantAsync(conversationId, userId, cancellationToken))
        {
            return null;
        }

        limit = Math.Clamp(limit, 1, 100);
        DateTimeOffset? before = null;
        if (beforeMessageId.HasValue)
        {
            var anchor = await chatRepository.GetMessageAsync(beforeMessageId.Value, cancellationToken);
            if (anchor is null || anchor.ConversationId != conversationId) return null;
            before = anchor.CreatedAt;
        }

        var rows = await chatRepository.ListMessagesAsync(conversationId, before, limit, cancellationToken);
        var reactions = await chatRepository.GetReactionsForMessagesAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        var reactionMap = reactions
            .GroupBy(r => r.MessageId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<ChatMessageReactionSummary>)g
                .Select(r => new ChatMessageReactionSummary(r.Emoji, r.Count, r.UserIds))
                .ToList());
        var readStates = (await chatRepository.GetParticipantReadStatesAsync(conversationId, cancellationToken))
            .Select(r => new ParticipantReadState(r.UserId, r.LastReadMessageId, r.LastReadAt))
            .ToList();

        var messages = rows.Select(row => MapMessageRow(row, reactionMap.TryGetValue(row.Id, out var r) ? r : [])).ToList();
        return new ChatMessagesPageResponse(conversationId, messages, rows.Count == limit, readStates);
    }

    public async Task<(ChatOutcome, ChatMessageResponse?, string?)> SendMessageAsync(Guid userId, Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        var conversation = await chatRepository.GetConversationAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return (ChatOutcome.ConversationNotFound, null, "Không tìm thấy hội thoại.");
        }

        var participant = conversation.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is null)
        {
            return (ChatOutcome.Forbidden, null, "Bạn không phải thành viên của hội thoại này.");
        }

        if (participant.LeftAt is not null)
        {
            return (ChatOutcome.Forbidden, null, "Bạn đã rời khỏi hội thoại này.");
        }

        if (participant.RequestStatus == ConversationRequestStatus.PENDING_REQUEST)
        {
            return (ChatOutcome.NotAccepted, null, "Bạn chưa chấp nhận hội thoại này.");
        }

        if (participant.RequestStatus == ConversationRequestStatus.DECLINED)
        {
            return (ChatOutcome.NotAccepted, null, "Bạn đã từ chối hội thoại này.");
        }

        var senderState = await chatRepository.GetUserChatStateAsync(userId, cancellationToken);
        if (senderState.HasValue && senderState.Value.ChatBanReason is not null
            && (senderState.Value.ChatBannedUntil is null || senderState.Value.ChatBannedUntil.Value > DateTimeOffset.UtcNow))
        {
            return (ChatOutcome.ChatBanned, null, $"Quyền nhắn tin của bạn đang bị tạm khóa. {senderState.Value.ChatBanReason}".Trim());
        }

        if (conversation.Type == ConversationType.DIRECT)
        {
            var otherUserIds = conversation.Participants.Select(p => p.UserId).Where(id => id != userId).ToList();
            foreach (var other in otherUserIds)
            {
                if (await chatRepository.IsBlockedBetweenAsync(userId, other, cancellationToken))
                {
                    return (ChatOutcome.BlockedByParticipant, null, "Không thể gửi tin nhắn vì hội thoại đã bị chặn.");
                }
            }
        }

        var messageType = ParseMessageType(request.MessageType);
        if (messageType is null)
        {
            return (ChatOutcome.InvalidOperation, null, "Loại tin nhắn không hợp lệ.");
        }

        ChatMessage message;
        try
        {
            message = messageType.Value switch
            {
                ChatMessageType.TEXT => await CreateTextMessageAsync(conversation.Id, userId, request, cancellationToken),
                ChatMessageType.IMAGE or ChatMessageType.FILE => await CreateAttachmentMessageAsync(conversation.Id, userId, messageType.Value, request, cancellationToken),
                ChatMessageType.CONTEXT_CARD => await CreateContextCardMessageAsync(conversation.Id, userId, request, cancellationToken),
                _ => throw new InvalidOperationException("Không thể gửi loại tin nhắn này."),
            };
        }
        catch (InvalidOperationException ex)
        {
            return (ChatOutcome.InvalidOperation, null, ex.Message);
        }

        if (request.ReplyToMessageId.HasValue)
        {
            var replyTarget = await chatRepository.GetMessageAsync(request.ReplyToMessageId.Value, cancellationToken);
            if (replyTarget is null || replyTarget.ConversationId != conversationId)
            {
                return (ChatOutcome.InvalidOperation, null, "Tin nhắn được trả lời không thuộc hội thoại này.");
            }
        }

        await chatRepository.AddMessageAsync(message, cancellationToken);
        // Dùng UtcNow: message.CreatedAt chỉ được stamp khi SaveChanges chạy.
        conversation.TouchLastMessage(message.Id, message.BuildPreview(), DateTimeOffset.UtcNow);
        await chatRepository.SaveAsync(cancellationToken);

        await NotifyRecipientsAsync(conversation, message, cancellationToken);
        await chatRepository.SaveAsync(cancellationToken); // Lưu Notification + Outbox vừa upsert.

        var response = await MapMessageAsync(message.Id, cancellationToken);
        var unreadCounts = await chatRepository.GetUnreadCountsForConversationAsync(conversationId, cancellationToken);
        var deliveries = conversation.Participants
            .Where(p => p.UserId != userId && p.LeftAt is null)
            .Select(p => new ChatMessageDelivery(p.UserId, unreadCounts.TryGetValue(p.UserId, out var count) ? count : 0))
            .ToList();

        await realtimeNotifier.NotifyMessageReceivedAsync(response!, deliveries, cancellationToken);
        return (ChatOutcome.Success, response, null);
    }

    public async Task<(ChatOutcome, ChatMessageResponse?, string?)> EditMessageAsync(Guid userId, Guid messageId, string content, CancellationToken cancellationToken)
    {
        var message = await chatRepository.GetMessageAsync(messageId, cancellationToken);
        if (message is null) return (ChatOutcome.MessageNotFound, null, "Không tìm thấy tin nhắn.");
        if (message.SenderUserId != userId) return (ChatOutcome.Forbidden, null, "Chỉ người gửi mới được chỉnh sửa tin nhắn.");

        var now = DateTimeOffset.UtcNow;
        if (!message.CanEdit(userId, now))
        {
            return (ChatOutcome.InvalidOperation, null, "Chỉ có thể chỉnh sửa tin nhắn văn bản trong vòng 15 phút sau khi gửi.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return (ChatOutcome.InvalidOperation, null, "Nội dung tin nhắn không được để trống.");
        }

        message.Edit(content, now);
        if (message.Conversation.LastMessageId == message.Id)
        {
            message.Conversation.TouchLastMessage(message.Id, message.BuildPreview(), message.CreatedAt);
        }
        await chatRepository.SaveAsync(cancellationToken);

        var response = await MapMessageAsync(messageId, cancellationToken);
        var participantIds = await GetActiveParticipantIdsAsync(message.ConversationId, cancellationToken);
        await realtimeNotifier.NotifyMessageUpdatedAsync(response!, participantIds, cancellationToken);
        return (ChatOutcome.Success, response, null);
    }

    public async Task<(ChatOutcome, ChatMessageResponse?)> DeleteMessageAsync(Guid userId, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await chatRepository.GetMessageAsync(messageId, cancellationToken);
        if (message is null) return (ChatOutcome.MessageNotFound, null);
        if (message.SenderUserId != userId) return (ChatOutcome.Forbidden, null);

        message.SoftDelete(DateTimeOffset.UtcNow);
        if (message.Conversation.LastMessageId == message.Id)
        {
            message.Conversation.TouchLastMessage(message.Id, "Tin nhắn đã được thu hồi", message.CreatedAt);
        }
        await chatRepository.SaveAsync(cancellationToken);

        var participantIds = await GetActiveParticipantIdsAsync(message.ConversationId, cancellationToken);
        await realtimeNotifier.NotifyMessageDeletedAsync(message.ConversationId, message.Id, participantIds, cancellationToken);
        return (ChatOutcome.Success, null);
    }

    public async Task<(ChatOutcome, ChatMessageResponse?, string?)> ToggleReactionAsync(Guid userId, Guid messageId, string emoji, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(emoji) || emoji.Length > 16)
        {
            return (ChatOutcome.InvalidOperation, null, "Biểu cảm không hợp lệ.");
        }

        var message = await chatRepository.GetMessageAsync(messageId, cancellationToken);
        if (message is null) return (ChatOutcome.MessageNotFound, null, "Không tìm thấy tin nhắn.");

        if (!await chatRepository.IsAcceptedParticipantAsync(message.ConversationId, userId, cancellationToken))
        {
            return (ChatOutcome.Forbidden, null, "Bạn không phải thành viên của hội thoại này.");
        }

        var existing = message.Reactions.FirstOrDefault(r => r.UserId == userId && r.Emoji == emoji);
        if (existing is not null)
        {
            message.Reactions.Remove(existing);
        }
        else
        {
            await chatRepository.AddReactionAsync(new ChatMessageReaction(message.Id, userId, emoji), cancellationToken);
        }

        await chatRepository.SaveAsync(cancellationToken);

        var participantIds = await GetActiveParticipantIdsAsync(message.ConversationId, cancellationToken);
        var reactions = await chatRepository.GetReactionsForMessagesAsync([message.Id], cancellationToken);
        var summary = reactions.FirstOrDefault(r => r.Emoji == emoji);
        await realtimeNotifier.NotifyReactionToggledAsync(
            message.ConversationId,
            message.Id,
            emoji,
            summary?.Count ?? 0,
            summary?.UserIds ?? Array.Empty<Guid>(),
            userId,
            participantIds,
            cancellationToken);

        var response = await MapMessageAsync(messageId, cancellationToken);
        return (ChatOutcome.Success, response, null);
    }

    public async Task<(ChatOutcome, ChatMessageResponse?)> MarkConversationReadAsync(Guid userId, Guid conversationId, Guid? messageId, CancellationToken cancellationToken)
    {
        var conversation = await chatRepository.GetConversationAsync(conversationId, cancellationToken);
        if (conversation is null) return (ChatOutcome.ConversationNotFound, null);

        var participant = conversation.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is null || participant.LeftAt is not null) return (ChatOutcome.Forbidden, null);

        var targetId = messageId ?? conversation.LastMessageId;
        if (targetId is null) return (ChatOutcome.Success, null);

        var changed = participant.MarkRead(targetId.Value);
        if (notificationWriter is not null)
        {
            await notificationWriter.MarkChatNotificationsReadAsync(userId, conversationId.ToString(), cancellationToken);
        }

        if (changed)
        {
            await chatRepository.SaveAsync(cancellationToken);
            var others = conversation.Participants
                .Where(p => p.UserId != userId && p.LeftAt is null)
                .Select(p => p.UserId)
                .ToList();
            await realtimeNotifier.NotifyConversationReadAsync(conversationId, userId, participant.LastReadAt ?? DateTimeOffset.UtcNow, others, cancellationToken);
            await realtimeNotifier.NotifyUnreadCountChangedAsync(conversationId, userId, 0, cancellationToken);
        }
        else if (notificationWriter is not null)
        {
            await chatRepository.SaveAsync(cancellationToken);
        }

        return (ChatOutcome.Success, null);
    }

    public async Task<ChatOutcome> AcceptConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await chatRepository.GetConversationAsync(conversationId, cancellationToken);
        if (conversation is null) return ChatOutcome.ConversationNotFound;

        var participant = conversation.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is null) return ChatOutcome.Forbidden;

        if (!participant.AcceptRequest())
        {
            return ChatOutcome.Success;
        }

        await chatRepository.SaveAsync(cancellationToken);

        var creatorId = conversation.CreatedByUserId;
        if (creatorId != userId)
        {
            var accepterName = (await chatRepository.GetUserDisplayAsync(userId, cancellationToken))?.DisplayName ?? "Ai đó";
            notificationWriter?.Add(new NotificationMessage(
                creatorId,
                "CHAT_REQUEST_ACCEPTED",
                $"{accepterName} đã chấp nhận tin nhắn của bạn",
                JsonSerializer.Serialize(new { conversationId })));
            await chatRepository.SaveAsync(cancellationToken);
        }

        var participantIds = conversation.Participants.Where(p => p.LeftAt is null).Select(p => p.UserId).ToList();
        await realtimeNotifier.NotifyConversationUpsertedAsync(conversationId, participantIds, cancellationToken);
        return ChatOutcome.Success;
    }

    public async Task<ChatOutcome> DeclineConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await chatRepository.GetConversationAsync(conversationId, cancellationToken);
        if (conversation is null) return ChatOutcome.ConversationNotFound;

        var participant = conversation.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is null) return ChatOutcome.Forbidden;

        participant.DeclineRequest();
        await chatRepository.SaveAsync(cancellationToken);
        return ChatOutcome.Success;
    }

    public async Task<(ChatOutcome, ChatAttachmentUploadUrlResponse?, string?)> CreateAttachmentUploadUrlAsync(Guid userId, ChatAttachmentUploadUrlRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Contains('/') || request.FileName.Contains('\\'))
        {
            return (ChatOutcome.InvalidAttachment, null, "Tên tệp không hợp lệ.");
        }

        if (request.SizeBytes <= 0 || request.SizeBytes > MaxAttachmentBytes)
        {
            return (ChatOutcome.InvalidAttachment, null, $"Tệp đính kèm tối đa {MaxAttachmentBytes / (1024 * 1024)}MB.");
        }

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        var isImage = ImageExtensions.Contains(extension);
        var isDocument = DocumentExtensions.Contains(extension);
        if (!isImage && !isDocument)
        {
            return (ChatOutcome.InvalidAttachment, null, "Định dạng tệp không được hỗ trợ.");
        }

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            return (ChatOutcome.InvalidAttachment, null, "Thiếu kiểu nội dung (Content-Type) của tệp.");
        }

        var storageKey = $"chat/{DateTimeOffset.UtcNow:yyyy/MM}/{Guid.CreateVersion7():N}{extension}";
        var uploadUrl = await fileStorage.CreateUploadUrlAsync(storageKey, request.ContentType, TimeSpan.FromMinutes(15), cancellationToken);
        var viewUrl = $"/api/v1/files/view?key={Uri.EscapeDataString(storageKey)}";
        return (ChatOutcome.Success, new ChatAttachmentUploadUrlResponse(uploadUrl.ToString(), storageKey, viewUrl, "PUT"), null);
    }

    public async Task<(ChatReportOutcome, string?)> ReportMessageAsync(Guid userId, ReportMessageRequest request, CancellationToken cancellationToken)
    {
        var message = await chatRepository.GetMessageAsync(request.MessageId, cancellationToken);
        if (message is null) return (ChatReportOutcome.MessageNotFound, "Không tìm thấy tin nhắn cần báo cáo.");

        if (!await chatRepository.IsParticipantAsync(message.ConversationId, userId, cancellationToken))
        {
            return (ChatReportOutcome.MessageNotFound, "Bạn không có quyền báo cáo tin nhắn này.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5)
        {
            return (ChatReportOutcome.MessageNotFound, "Vui lòng mô tả lý do báo cáo (ít nhất 5 ký tự).");
        }

        var hasOpenReport = await chatRepository.HasOpenReportAsync(message.Id, userId, cancellationToken);
        if (hasOpenReport)
        {
            return (ChatReportOutcome.AlreadyReported, "Bạn đã báo cáo tin nhắn này rồi.");
        }

        await chatRepository.AddReportAsync(new ChatReport(message.Id, userId, request.Reason), cancellationToken);
        await chatRepository.SaveAsync(cancellationToken);
        return (ChatReportOutcome.Success, null);
    }

    public async Task<IReadOnlyList<ChatDirectoryUser>> SearchPeopleAsync(Guid userId, string? query, CancellationToken cancellationToken)
    {
        var rows = await chatRepository.SearchDirectoryAsync(userId, query, 12, cancellationToken);
        return rows.Select(row => new ChatDirectoryUser(
            row.UserId,
            row.DisplayName,
            row.AvatarUrl,
            row.Role,
            row.Detail,
            presenceTracker.IsOnline(row.UserId),
            row.ExistingConversationId)).ToList();
    }

    // ---------- Helpers ----------

    private async Task<ConversationParticipant?> GetAcceptedParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        var conversation = await chatRepository.GetConversationAsync(conversationId, cancellationToken);
        return conversation?.Participants.FirstOrDefault(p => p.UserId == userId && p.LeftAt is null && p.RequestStatus == ConversationRequestStatus.ACCEPTED);
    }

    private async Task<IReadOnlyCollection<Guid>> GetActiveParticipantIdsAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var statuses = await chatRepository.GetParticipantStatusesAsync(conversationId, cancellationToken);
        return statuses.Where(p => p.LeftAt is null && p.Status != ConversationRequestStatus.DECLINED).Select(p => p.UserId).ToList();
    }

    /// <summary>
    /// Đồng bộ với hệ thống chuông thông báo chung: mỗi tin nhắn thường (TEXT/IMAGE/FILE/CONTEXT_CARD)
    /// tạo hoặc cập nhật 1 thông báo chưa đọc CHO MỖI NGƯỜI NHẬN (trừ người gửi, người đã mute,
    /// hội thoại chờ chấp nhận đã có CHAT_REQUEST riêng). Chuông realtime làm mới qua outbox.
    /// </summary>
    private async Task NotifyRecipientsAsync(Conversation conversation, ChatMessage message, CancellationToken cancellationToken)
    {
        if (notificationWriter is null || message.MessageType == ChatMessageType.SYSTEM) return;

        var sender = await chatRepository.GetUserDisplayAsync(message.SenderUserId, cancellationToken);
        var senderName = sender?.DisplayName ?? "Ai đó";
        var preview = message.BuildPreview();
        if (preview.Length > 120) preview = preview[..120] + "…";
        var title = $"{senderName}: {preview}";
        if (title.Length > 300) title = title[..300];

        foreach (var participant in conversation.Participants)
        {
            if (participant.UserId == message.SenderUserId) continue;
            if (participant.LeftAt is not null) continue;
            if (participant.RequestStatus == ConversationRequestStatus.PENDING_REQUEST) continue;
            if (participant.IsMuted) continue;

            // URL đích theo portal của NGƯỜI NHẬN để bấm chuông mở đúng hộp thư.
            var role = await chatRepository.ResolveProfileRoleAsync(participant.UserId, cancellationToken);
            var inboxBase = role switch
            {
                "COMPANY" => "/company/messages",
                "LECTURER" => "/lecturer/messages",
                _ => "/student/messages",
            };
            var payload = JsonSerializer.Serialize(new
            {
                conversationId = conversation.Id,
                messageId = message.Id,
                senderUserId = message.SenderUserId,
                senderName,
                preview,
                kind = "chat_message",
                url = $"{inboxBase}?c={conversation.Id}",
            });

            await notificationWriter.UpsertUnreadChatNotificationAsync(participant.UserId, conversation.Id.ToString(), title, payload, cancellationToken);
        }
    }

    private async Task BroadcastConversationUpsertAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        var participantIds = conversation.Participants.Where(p => p.LeftAt is null).Select(p => p.UserId).ToList();
        await realtimeNotifier.NotifyConversationUpsertedAsync(conversation.Id, participantIds, cancellationToken);
    }

    private async Task SyncProjectParticipantsAsync(Conversation conversation, ProjectConversationMembers membership, CancellationToken cancellationToken)
    {
        var expectedUserIds = membership.CompanyUserIds
            .Concat(membership.LecturerUserIds)
            .Concat(membership.StudentUserIds)
            .ToHashSet();

        // Thêm người mới / cho người từng rời phòng quay lại.
        foreach (var userId in expectedUserIds)
        {
            var participant = conversation.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant is null)
            {
                await chatRepository.AddParticipantAsync(new ConversationParticipant(conversation.Id, userId, ConversationParticipantRole.MEMBER), cancellationToken);
            }
            else if (participant.LeftAt is not null)
            {
                participant.Rejoin();
            }
        }

        // Cho người đã bị loại khỏi dự án rời phòng (giữ lịch sử).
        foreach (var participant in conversation.Participants.Where(p => p.LeftAt is null && !expectedUserIds.Contains(p.UserId)).ToList())
        {
            participant.Leave();
        }
    }

    private async Task<ChatMessage> CreateTextMessageAsync(Guid conversationId, Guid userId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new InvalidOperationException("Nội dung tin nhắn không được để trống.");
        }

        await ValidateReplyAsync(conversationId, request.ReplyToMessageId, cancellationToken);
        return new ChatMessage(conversationId, userId, ChatMessageType.TEXT, request.Content.Trim(), null, request.ReplyToMessageId);
    }

    private async Task<ChatMessage> CreateAttachmentMessageAsync(Guid conversationId, Guid userId, ChatMessageType type, SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MetadataJson))
        {
            throw new InvalidOperationException("Thiếu thông tin đính kèm.");
        }

        AttachmentMetadata metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<AttachmentMetadata>(request.MetadataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Thông tin đính kèm không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(metadata.StorageKey) || !metadata.StorageKey.StartsWith("chat/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Tệp đính kèm phải được tải lên qua kênh chat.");
        }

        if (metadata.SizeBytes is < 1 or > MaxAttachmentBytes)
        {
            throw new InvalidOperationException($"Tệp đính kèm tối đa {MaxAttachmentBytes / (1024 * 1024)}MB.");
        }

        var stored = await fileStorage.GetMetadataAsync(metadata.StorageKey, cancellationToken);
        if (stored is null)
        {
            throw new InvalidOperationException("Tệp chưa được tải lên thành công, vui lòng thử lại.");
        }

        if (stored.Length > MaxAttachmentBytes)
        {
            throw new InvalidOperationException($"Tệp đính kèm tối đa {MaxAttachmentBytes / (1024 * 1024)}MB.");
        }

        await ValidateReplyAsync(conversationId, request.ReplyToMessageId, cancellationToken);
        var normalized = JsonSerializer.Serialize(new
        {
            storageKey = metadata.StorageKey,
            fileName = string.IsNullOrWhiteSpace(metadata.FileName) ? "file" : metadata.FileName,
            sizeBytes = stored.Length,
            mimeType = string.IsNullOrWhiteSpace(metadata.MimeType) ? stored.DetectedContentType.ToString() : metadata.MimeType,
        });
        return new ChatMessage(conversationId, userId, type, string.IsNullOrWhiteSpace(request.Content) ? metadata.FileName ?? "Tệp đính kèm" : request.Content.Trim(), normalized, request.ReplyToMessageId);
    }

    private async Task<ChatMessage> CreateContextCardMessageAsync(Guid conversationId, Guid userId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MetadataJson))
        {
            throw new InvalidOperationException("Thiếu thông tin thẻ ngữ cảnh.");
        }

        ContextCardMetadata card;
        try
        {
            card = JsonSerializer.Deserialize<ContextCardMetadata>(request.MetadataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Thẻ ngữ cảnh không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(card.CardType) || card.EntityId is null || Guid.Empty == card.EntityId || string.IsNullOrWhiteSpace(card.Title))
        {
            throw new InvalidOperationException("Thẻ ngữ cảnh thiếu loại, mã đối tượng hoặc tiêu đề.");
        }

        var allowed = new[] { "PROJECT", "MEETING", "APPLICATION", "MEETING_MINUTE" };
        if (!allowed.Contains(card.CardType.ToUpperInvariant()))
        {
            throw new InvalidOperationException("Loại thẻ ngữ cảnh không được hỗ trợ.");
        }

        await ValidateReplyAsync(conversationId, request.ReplyToMessageId, cancellationToken);
        var normalized = JsonSerializer.Serialize(new
        {
            cardType = card.CardType.ToUpperInvariant(),
            entityId = card.EntityId,
            title = card.Title,
            subtitle = card.Subtitle,
            linkUrl = card.LinkUrl,
            extra = card.Extra,
        });
        return new ChatMessage(conversationId, userId, ChatMessageType.CONTEXT_CARD, $"Đã chia sẻ thẻ ngữ cảnh: {card.Title}", normalized, request.ReplyToMessageId);
    }

    private async Task ValidateReplyAsync(Guid conversationId, Guid? replyToMessageId, CancellationToken cancellationToken)
    {
        if (!replyToMessageId.HasValue) return;
        var replyTarget = await chatRepository.GetMessageAsync(replyToMessageId.Value, cancellationToken);
        if (replyTarget is null || replyTarget.ConversationId != conversationId)
        {
            throw new InvalidOperationException("Tin nhắn được trả lời không thuộc hội thoại này.");
        }
    }

    private async Task<ChatMessageResponse> MapMessageAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var row = await chatRepository.GetMessageRowAsync(messageId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy tin nhắn vừa tạo.");
        var reactions = await chatRepository.GetReactionsForMessagesAsync([messageId], cancellationToken);
        return MapMessageRow(row, reactions.Select(r => new ChatMessageReactionSummary(r.Emoji, r.Count, r.UserIds)).ToList());
    }

    private static ChatMessageResponse MapMessageRow(ChatMessageRow row, IReadOnlyCollection<ChatMessageReactionSummary> reactions) =>
        new(
            row.Id,
            row.ConversationId,
            row.SenderUserId,
            row.SenderDisplayName,
            row.SenderAvatarUrl,
            row.MessageType.ToString(),
            row.Content,
            row.MetadataJson,
            row.ReplyToMessageId,
            row.ReplyToPreview,
            row.ReplyToSenderName,
            row.IsEdited,
            row.EditedAt,
            row.IsDeleted,
            reactions,
            row.CreatedAt);

    private static ChatConversationResponse MapConversationRow(ChatConversationRow row, IChatPresenceTracker presenceTracker) =>
        new(
            row.Id,
            row.Type.ToString(),
            row.Title,
            row.ProjectId,
            row.ProjectId is null ? null : new ChatProjectSummary(row.ProjectId.Value, row.ProjectTitle ?? string.Empty),
            row.AvatarUrl,
            row.OtherUserId is null ? null : new ChatUserSummary(
                row.OtherUserId.Value,
                row.OtherDisplayName ?? string.Empty,
                row.OtherAvatarUrl,
                row.OtherRole,
                presenceTracker.IsOnline(row.OtherUserId.Value),
                null),
            row.LastMessage,
            row.UnreadCount,
            row.IsMuted,
            row.IsPinned,
            row.RequestStatus?.ToString(),
            row.ParticipantsCount,
            row.CreatedAt,
            row.LastMessageAt);

    private static ConversationInboxTab ParseTab(string? tab) => tab?.ToUpperInvariant() switch
    {
        "UNREAD" => ConversationInboxTab.UNREAD,
        "GROUPS" => ConversationInboxTab.GROUPS,
        "REQUESTS" => ConversationInboxTab.REQUESTS,
        _ => ConversationInboxTab.ALL,
    };

    private static ChatMessageType? ParseMessageType(string? messageType) => messageType?.ToUpperInvariant() switch
    {
        "TEXT" => ChatMessageType.TEXT,
        "IMAGE" => ChatMessageType.IMAGE,
        "FILE" => ChatMessageType.FILE,
        "CONTEXT_CARD" => ChatMessageType.CONTEXT_CARD,
        _ => null,
    };

    private async Task<bool> IsAdminUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await chatRepository.IsAdminUserAsync(userId, cancellationToken);

    private sealed record AttachmentMetadata(string? StorageKey, string? FileName, long? SizeBytes, string? MimeType);

    private sealed record ContextCardMetadata(string? CardType, Guid? EntityId, string? Title, string? Subtitle, string? LinkUrl, string? Extra);
}
