using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Chat;

/// <summary>
/// Hội thoại chat: Direct (1-1, duy nhất một hội thoại cho mỗi cặp người dùng)
/// hoặc ProjectGroup (nhóm dự án 3 bên Doanh nghiệp + GVHD + Sinh viên).
/// </summary>
public sealed class Conversation : AuditableEntity
{
    public ConversationType Type { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string? Title { get; private set; }
    public string? AvatarUrl { get; private set; }

    /// <summary>Khóa chuẩn hóa cho hội thoại Direct: min(u1,u2)_max(u1,u2) — duy nhất khi Type=Direct.</summary>
    public string? DirectPairKey { get; private set; }

    public Guid? LastMessageId { get; private set; }
    public string? LastMessagePreview { get; private set; }
    public DateTimeOffset? LastMessageAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public DNTU.SkillBridge.Domain.Projects.Project? Project { get; private set; }
    public ChatMessage? LastMessage { get; private set; }

    public ICollection<ConversationParticipant> Participants { get; } = [];
    public ICollection<ChatMessage> Messages { get; } = [];

    private Conversation() { }

    /// <summary>Tạo hội thoại Direct với khóa cặp chuẩn hóa (không phân biệt thứ tự).</summary>
    public static Conversation CreateDirect(Guid requesterUserId, Guid targetUserId, Guid createdByUserId, ConversationRequestStatus targetStatus = ConversationRequestStatus.ACCEPTED)
    {
        var pairKey = BuildDirectPairKey(requesterUserId, targetUserId);
        var conversation = new Conversation
        {
            Type = ConversationType.DIRECT,
            DirectPairKey = pairKey,
            CreatedByUserId = createdByUserId,
        };
        conversation.Participants.Add(new ConversationParticipant(conversation.Id, requesterUserId, ConversationParticipantRole.OWNER));
        conversation.Participants.Add(new ConversationParticipant(conversation.Id, targetUserId, ConversationParticipantRole.MEMBER, targetStatus));
        return conversation;
    }

    public static Conversation CreateProjectGroup(Guid projectId, string title, Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Project group title is required.", nameof(title));
        return new Conversation
        {
            Type = ConversationType.PROJECT_GROUP,
            ProjectId = projectId,
            Title = title.Trim(),
            CreatedByUserId = createdByUserId,
        };
    }

    public static string BuildDirectPairKey(Guid userA, Guid userB)
    {
        var (first, second) = userA.CompareTo(userB) <= 0 ? (userA, userB) : (userB, userA);
        return $"{first:N}_{second:N}";
    }

    /// <summary>Cập nhật vùng nhớ đệm "tin nhắn cuối" dùng cho hộp thư.</summary>
    public void TouchLastMessage(Guid messageId, string preview, DateTimeOffset sentAt)
    {
        LastMessageId = messageId;
        LastMessagePreview = preview.Length > 240 ? preview[..240] : preview;
        LastMessageAt = sentAt;
    }

    public void SetTitle(string title)
    {
        if (!string.IsNullOrWhiteSpace(title)) Title = title.Trim();
    }

    public void SetAvatarUrl(string? avatarUrl) => AvatarUrl = avatarUrl;
}

/// <summary>
/// Thành viên hội thoại. Composite PK (ConversationId, UserId).
/// RequestStatus điều khiển cơ chế Tin nhắn chờ cho hội thoại Direct từ người lạ.
/// </summary>
public sealed class ConversationParticipant : AuditableEntity
{
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public ConversationParticipantRole Role { get; private set; }
    public ConversationRequestStatus RequestStatus { get; private set; }
    public Guid? LastReadMessageId { get; private set; }
    public DateTimeOffset? LastReadAt { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsPinned { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? LeftAt { get; private set; }

    public Conversation Conversation { get; private set; } = null!;
    public DNTU.SkillBridge.Domain.Identity.User User { get; private set; } = null!;

    private ConversationParticipant() { }

    public ConversationParticipant(Guid conversationId, Guid userId, ConversationParticipantRole role, ConversationRequestStatus requestStatus = ConversationRequestStatus.ACCEPTED)
    {
        ConversationId = conversationId;
        UserId = userId;
        Role = role;
        RequestStatus = requestStatus;
        JoinedAt = DateTimeOffset.UtcNow;
    }

    public bool IsActiveMember => LeftAt is null && RequestStatus != ConversationRequestStatus.DECLINED;

    /// <summary>Người nhận chấp nhận tin nhắn chờ.</summary>
    public bool AcceptRequest()
    {
        if (RequestStatus == ConversationRequestStatus.ACCEPTED) return false;
        RequestStatus = ConversationRequestStatus.ACCEPTED;
        LeftAt = null;
        return true;
    }

    /// <summary>Người nhận từ chối tin nhắn chờ (hội thoại bị ẩn khỏi hộp thư của họ).</summary>
    public bool DeclineRequest()
    {
        if (RequestStatus == ConversationRequestStatus.DECLINED) return false;
        RequestStatus = ConversationRequestStatus.DECLINED;
        return true;
    }

    public bool MarkRead(Guid messageId)
    {
        if (LastReadMessageId == messageId) return false;
        LastReadMessageId = messageId;
        LastReadAt = DateTimeOffset.UtcNow;
        return true;
    }

    public void SetPinned(bool pinned) => IsPinned = pinned;

    public void SetMuted(bool muted) => IsMuted = muted;

    public void Leave() => LeftAt = DateTimeOffset.UtcNow;

    public void Rejoin() => LeftAt = null;
}

/// <summary>Tin nhắn chat (soft-delete, hỗ trợ reply + thẻ ngữ cảnh).</summary>
public sealed class ChatMessage : AuditableEntity
{
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(15);

    public Guid ConversationId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public ChatMessageType MessageType { get; private set; }
    public string Content { get; private set; } = string.Empty;

    /// <summary>JSON mở rộng cho IMAGE/FILE (storageKey, fileName, size, mimeType) hoặc CONTEXT_CARD (cardType, entityId, title, linkUrl).</summary>
    public string? MetadataJson { get; private set; }

    public Guid? ReplyToMessageId { get; private set; }
    public bool IsEdited { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public Conversation Conversation { get; private set; } = null!;
    public DNTU.SkillBridge.Domain.Identity.User SenderUser { get; private set; } = null!;
    public ChatMessage? ReplyTo { get; private set; }
    public ICollection<ChatMessageReaction> Reactions { get; } = [];

    private ChatMessage() { }

    public ChatMessage(Guid conversationId, Guid senderUserId, ChatMessageType messageType, string content, string? metadataJson = null, Guid? replyToMessageId = null)
    {
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Message content is required.", nameof(content));
        ConversationId = conversationId;
        SenderUserId = senderUserId;
        MessageType = messageType;
        Content = content.Length > 4000 ? content[..4000] : content;
        MetadataJson = metadataJson;
        ReplyToMessageId = replyToMessageId;
    }

    public static ChatMessage SystemMessage(Guid conversationId, string content) =>
        new(conversationId, Guid.Empty, ChatMessageType.SYSTEM, content);

    public bool CanEdit(Guid userId, DateTimeOffset now) =>
        !IsDeleted
        && SenderUserId == userId
        && MessageType == ChatMessageType.TEXT
        && now - CreatedAt <= EditWindow;

    public void Edit(string newContent, DateTimeOffset now)
    {
        if (!CanEdit(SenderUserId, now)) throw new InvalidOperationException("Message can no longer be edited.");
        Content = newContent.Length > 4000 ? newContent[..4000] : newContent;
        IsEdited = true;
        EditedAt = now;
    }

    public void SoftDelete(DateTimeOffset now)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = now;
        Content = string.Empty;
        MetadataJson = null;
    }

    /// <summary>Preview hiển thị trong danh sách hội thoại.</summary>
    public string BuildPreview() => MessageType switch
    {
        ChatMessageType.TEXT => Content,
        ChatMessageType.IMAGE => "[Ảnh]",
        ChatMessageType.FILE => "[Tệp đính kèm]",
        ChatMessageType.CONTEXT_CARD => "[Thẻ ngữ cảnh]",
        ChatMessageType.SYSTEM => Content,
        _ => Content,
    };
}

/// <summary>Biểu cảm trên tin nhắn — duy nhất (MessageId, UserId, Emoji).</summary>
public sealed class ChatMessageReaction : AuditableEntity
{
    public Guid MessageId { get; private set; }
    public Guid UserId { get; private set; }
    public string Emoji { get; private set; } = string.Empty;

    public ChatMessage Message { get; private set; } = null!;

    private ChatMessageReaction() { }

    public ChatMessageReaction(Guid messageId, Guid userId, string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji) || emoji.Length > 16) throw new ArgumentException("Emoji must be 1-16 characters.", nameof(emoji));
        MessageId = messageId;
        UserId = userId;
        Emoji = emoji;
    }
}
