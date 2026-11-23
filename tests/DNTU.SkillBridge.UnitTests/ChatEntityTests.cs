using DNTU.SkillBridge.Domain.Chat;
using DNTU.SkillBridge.Domain.Social;

namespace DNTU.SkillBridge.UnitTests;

/// <summary>Domain invariant của chat: pair key, cửa sổ chỉnh sửa, trạng thái tin nhắn chờ.</summary>
public sealed class ChatEntityTests
{
    [Fact]
    public void BuildDirectPairKey_IsOrderIndependent()
    {
        var a = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var b = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var keyAB = Conversation.BuildDirectPairKey(a, b);
        var keyBA = Conversation.BuildDirectPairKey(b, a);

        Assert.Equal(keyAB, keyBA);
        Assert.Contains(a.ToString("N"), keyAB);
        Assert.Contains(b.ToString("N"), keyAB);
        Assert.True(keyAB.IndexOf(a.ToString("N"), StringComparison.Ordinal) < keyAB.IndexOf(b.ToString("N"), StringComparison.Ordinal));
    }

    [Fact]
    public void CreateDirect_BothParticipantsAdded_RequesterOwner()
    {
        var requester = Guid.NewGuid();
        var target = Guid.NewGuid();

        var conversation = Conversation.CreateDirect(requester, target, requester);

        Assert.Equal(ConversationType.DIRECT, conversation.Type);
        Assert.Equal(2, conversation.Participants.Count);
        var owner = conversation.Participants.Single(p => p.UserId == requester);
        Assert.Equal(ConversationParticipantRole.OWNER, owner.Role);
        Assert.Equal(ConversationRequestStatus.ACCEPTED, owner.RequestStatus);
        var member = conversation.Participants.Single(p => p.UserId == target);
        Assert.Equal(ConversationRequestStatus.ACCEPTED, member.RequestStatus);
    }

    [Fact]
    public void CreateDirect_WithPendingStatus_MarksTargetPendingRequest()
    {
        var conversation = Conversation.CreateDirect(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationRequestStatus.PENDING_REQUEST);
        var target = conversation.Participants.Single(p => p.Role == ConversationParticipantRole.MEMBER);
        Assert.Equal(ConversationRequestStatus.PENDING_REQUEST, target.RequestStatus);
        // PENDING vẫn là thành viên còn ở trong hội thoại (chưa bị từ chối/rời phòng).
        Assert.True(target.IsActiveMember);
        Assert.False(target.AcceptRequest() == false && target.RequestStatus != ConversationRequestStatus.ACCEPTED);
    }

    [Fact]
    public void AcceptRequest_TransitionsToAccepted_AndIsIdempotent()
    {
        var participant = Conversation.CreateDirect(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationRequestStatus.PENDING_REQUEST)
            .Participants.Single(p => p.Role == ConversationParticipantRole.MEMBER);

        Assert.True(participant.AcceptRequest());
        Assert.Equal(ConversationRequestStatus.ACCEPTED, participant.RequestStatus);
        Assert.True(participant.IsActiveMember);
        Assert.False(participant.AcceptRequest(), "Accept lần hai phải idempotent.");
    }

    [Fact]
    public void DeclineRequest_MakesParticipantInactive()
    {
        var participant = Conversation.CreateDirect(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationRequestStatus.PENDING_REQUEST)
            .Participants.Single(p => p.Role == ConversationParticipantRole.MEMBER);

        Assert.True(participant.DeclineRequest());
        Assert.Equal(ConversationRequestStatus.DECLINED, participant.RequestStatus);
        Assert.False(participant.IsActiveMember);
    }

    [Fact]
    public void TextMessage_CanEditWithinWindow_ThenThrows()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new ChatMessage(Guid.NewGuid(), Guid.NewGuid(), ChatMessageType.TEXT, "Xin chào", null);
        message.CreatedAt = now; // Thực tế được stamp khi SaveChanges.

        Assert.True(message.CanEdit(message.SenderUserId, now.AddMinutes(10)));
        message.Edit("Đã chỉnh sửa", now.AddMinutes(10));
        Assert.True(message.IsEdited);
        Assert.Equal("Đã chỉnh sửa", message.Content);

        Assert.False(message.CanEdit(message.SenderUserId, now.AddMinutes(16)));
        Assert.Throws<InvalidOperationException>(() => message.Edit("Quá hạn", now.AddMinutes(16)));
    }

    [Fact]
    public void Edit_RejectsOtherUserAndNonTextTypes()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new ChatMessage(Guid.NewGuid(), Guid.NewGuid(), ChatMessageType.TEXT, "Hello", null);

        Assert.False(message.CanEdit(Guid.NewGuid(), now.AddMinutes(1)));

        var image = new ChatMessage(Guid.NewGuid(), Guid.NewGuid(), ChatMessageType.IMAGE, "anh.png", "{}");
        Assert.False(image.CanEdit(image.SenderUserId, now.AddMinutes(1)));
    }

    [Fact]
    public void SoftDelete_HidesContent_AndSetsFlag()
    {
        var message = new ChatMessage(Guid.NewGuid(), Guid.NewGuid(), ChatMessageType.TEXT, "Nội dung cần thu hồi", null);

        message.SoftDelete(DateTimeOffset.UtcNow);

        Assert.True(message.IsDeleted);
        Assert.Equal(string.Empty, message.Content);
        Assert.Equal(string.Empty, message.BuildPreview());
    }

    [Theory]
    [InlineData(ChatMessageType.IMAGE, "[Ảnh]")]
    [InlineData(ChatMessageType.FILE, "[Tệp đính kèm]")]
    [InlineData(ChatMessageType.CONTEXT_CARD, "[Thẻ ngữ cảnh]")]
    public void BuildPreview_DescribesNonTextTypes(ChatMessageType type, string expected)
    {
        var message = new ChatMessage(Guid.NewGuid(), Guid.NewGuid(), type, "payload", null);
        Assert.Equal(expected, message.BuildPreview());
    }

    [Fact]
    public void TouchLastMessage_TruncatesPreviewTo240Chars()
    {
        var conversation = Conversation.CreateDirect(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var longPreview = new string('x', 400);

        conversation.TouchLastMessage(Guid.NewGuid(), longPreview, DateTimeOffset.UtcNow);

        Assert.Equal(240, conversation.LastMessagePreview!.Length);
        Assert.NotNull(conversation.LastMessageAt);
    }

    [Fact]
    public void SystemMessage_UsesEmptySender()
    {
        var message = ChatMessage.SystemMessage(Guid.NewGuid(), "Nhóm đã được tạo");
        Assert.Equal(Guid.Empty, message.SenderUserId);
        Assert.Equal(ChatMessageType.SYSTEM, message.MessageType);
    }

    [Fact]
    public void ChatReport_ResolveIsSingleTransition()
    {
        var report = new ChatReport(Guid.NewGuid(), Guid.NewGuid(), "Nội dung spam quảng cáo");
        Assert.Equal(ChatReportStatus.OPEN, report.Status);

        Assert.True(report.Resolve(Guid.NewGuid(), ChatReportDecision.WARNED, "Cảnh cáo lần 1"));
        Assert.Equal(ChatReportStatus.RESOLVED, report.Status);
        Assert.NotNull(report.ResolvedAt);

        Assert.False(report.Resolve(Guid.NewGuid(), ChatReportDecision.DISMISSED, null), "Không được resolve hai lần.");
    }

    [Fact]
    public void UserFollow_RejectsSelfFollow()
    {
        var userId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new UserFollow(userId, userId));
    }
}
