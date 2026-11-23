using DNTU.SkillBridge.Domain.Chat;
using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Social;

/// <summary>
/// Cạnh theo dõi đa chiều giữa các tài khoản (DN ↔ GV ↔ SV). Composite PK (Follower, Followee).
/// </summary>
public sealed class UserFollow : AuditableEntity
{
    public Guid FollowerUserId { get; private set; }
    public Guid FolloweeUserId { get; private set; }

    /// <summary>Người theo dõi có muốn nhận thông báo khi đối tượng có cập nhật quan trọng không.</summary>
    public bool NotifyOnUpdates { get; private set; } = true;

    private UserFollow() { }

    public UserFollow(Guid followerUserId, Guid followeeUserId, bool notifyOnUpdates = true)
    {
        if (followerUserId == followeeUserId) throw new ArgumentException("Users cannot follow themselves.", nameof(followeeUserId));
        FollowerUserId = followerUserId;
        FolloweeUserId = followeeUserId;
        NotifyOnUpdates = notifyOnUpdates;
    }

    public void SetNotifyOnUpdates(bool notifyOnUpdates) => NotifyOnUpdates = notifyOnUpdates;
}

/// <summary>Bản ghi hoạt động dùng cho Bảng tin (Following Feed).</summary>
public sealed class SocialActivity : AuditableEntity
{
    public Guid ActorUserId { get; private set; }
    public SocialActivityType ActivityType { get; private set; }
    public Guid? TargetEntityId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string LinkUrl { get; private set; } = string.Empty;

    private SocialActivity() { }

    public SocialActivity(Guid actorUserId, SocialActivityType activityType, string title, string summary, string linkUrl, Guid? targetEntityId = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Activity title is required.", nameof(title));
        ActorUserId = actorUserId;
        ActivityType = activityType;
        TargetEntityId = targetEntityId;
        Title = title.Trim();
        Summary = summary.Trim();
        LinkUrl = linkUrl.Trim();
    }
}

/// <summary>Quan hệ chặn hai phía — hủy Follow cả hai chiều và chặn gửi tin mới.</summary>
public sealed class UserBlock : AuditableEntity
{
    public Guid BlockerUserId { get; private set; }
    public Guid BlockedUserId { get; private set; }

    private UserBlock() { }

    public UserBlock(Guid blockerUserId, Guid blockedUserId)
    {
        if (blockerUserId == blockedUserId) throw new ArgumentException("Users cannot block themselves.", nameof(blockerUserId));
        BlockerUserId = blockerUserId;
        BlockedUserId = blockedUserId;
    }
}

/// <summary>Báo cáo vi phạm tin nhắn gửi về hàng đợi kiểm duyệt của Admin.</summary>
public sealed class ChatReport : AuditableEntity
{
    public Guid MessageId { get; private set; }
    public Guid ReporterUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public ChatReportStatus Status { get; private set; }
    public Guid? ResolvedByAdminUserId { get; private set; }
    public string? ResolutionNote { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public ChatMessage Message { get; private set; } = null!;
    public DNTU.SkillBridge.Domain.Identity.User ReporterUser { get; private set; } = null!;

    private ChatReport() { }

    public ChatReport(Guid messageId, Guid reporterUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Report reason is required.", nameof(reason));
        MessageId = messageId;
        ReporterUserId = reporterUserId;
        Reason = reason.Trim();
        Status = ChatReportStatus.OPEN;
    }

    public bool Resolve(Guid adminUserId, ChatReportDecision decision, string? note)
    {
        if (Status != ChatReportStatus.OPEN) return false;
        Status = decision == ChatReportDecision.DISMISSED ? ChatReportStatus.DISMISSED : ChatReportStatus.RESOLVED;
        ResolvedByAdminUserId = adminUserId;
        ResolutionNote = note;
        ResolvedAt = DateTimeOffset.UtcNow;
        return true;
    }
}
