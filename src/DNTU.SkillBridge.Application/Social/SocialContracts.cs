using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Social;

namespace DNTU.SkillBridge.Application.Social;

// ---------- DTO ----------

public sealed record SocialActorSummary(Guid UserId, string DisplayName, string? AvatarUrl, string? Role);

public sealed record SocialFollowStatus(
    Guid UserId,
    bool IsFollowing,
    bool IsFollowedBy,
    bool IsMutual,
    int FollowersCount,
    int FollowingCount);

public sealed record SocialNetworkUser(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    string? Detail,
    bool IsFollowing,
    bool IsFollowedBy,
    DateTimeOffset FollowedAt);

public sealed record SocialFeedItem(
    Guid Id,
    string ActivityType,
    string Title,
    string Summary,
    string LinkUrl,
    Guid? TargetEntityId,
    SocialActorSummary Actor,
    DateTimeOffset CreatedAt);

public sealed record SocialSuggestionUser(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    string Reason,
    int SharedCount,
    bool IsFollowing);

/// <summary>Dữ liệu hồ sơ công khai tóm tắt của một người dùng (cho popup avatar / trang profile).</summary>
public sealed record SocialUserProfileData(Guid UserId, string DisplayName, string? AvatarUrl, string Role, string? Detail);

public sealed record SocialProfileSummary(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    string? Detail,
    int FollowersCount,
    int FollowingCount,
    bool IsFollowing,
    bool IsFollowedBy,
    bool IsMutual);

public sealed record SocialNetworkRow(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    string? Detail,
    bool ViewerFollows,
    DateTimeOffset FollowedAt);

public sealed record SocialActivityRow(
    Guid Id,
    SocialActivityType ActivityType,
    Guid? TargetEntityId,
    string Title,
    string Summary,
    string LinkUrl,
    DateTimeOffset CreatedAt,
    Guid ActorUserId,
    string ActorDisplayName,
    string? ActorAvatarUrl,
    string? ActorRole);

public sealed record SocialSuggestionRow(Guid UserId, string DisplayName, string? AvatarUrl, string Role, string Reason, int SharedCount, bool ViewerFollows);

// ---------- Outcome ----------

public enum SocialFollowOutcome
{
    Success = 0,
    TargetNotFound = 1,
    SelfFollow = 2,
    Blocked = 3,
}

public enum SocialBlockOutcome
{
    Success = 0,
    TargetNotFound = 1,
    SelfBlock = 2,
}

// ---------- Writer cho các feature khác ghi hoạt động ----------

public sealed record SocialActivityMessage(Guid ActorUserId, SocialActivityType ActivityType, string Title, string Summary, string LinkUrl, Guid? TargetEntityId = null);

/// <summary>Ghi SocialActivity trong cùng DbContext/transaction với caller (mirror INotificationWriter).</summary>
public interface ISocialActivityWriter
{
    void Add(SocialActivityMessage message);
}

// ---------- Service ----------

public interface ISocialService
{
    Task<(SocialFollowOutcome Outcome, SocialFollowStatus? Status, string? ErrorMessage)> FollowAsync(Guid followerUserId, Guid targetUserId, CancellationToken cancellationToken);

    Task<SocialFollowStatus> UnfollowAsync(Guid followerUserId, Guid targetUserId, CancellationToken cancellationToken);

    Task<SocialFollowStatus> GetFollowStatusAsync(Guid viewerUserId, Guid targetUserId, CancellationToken cancellationToken);

    Task<PagedResponse<SocialNetworkUser>> GetNetworkAsync(Guid viewerUserId, Guid userId, string direction, string? roleFilter, int page, int pageSize, CancellationToken cancellationToken);

    Task<PagedResponse<SocialFeedItem>> GetFeedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<SocialSuggestionUser>> GetSuggestionsAsync(Guid userId, int limit, CancellationToken cancellationToken);

    Task<(SocialBlockOutcome Outcome, string? ErrorMessage)> BlockUserAsync(Guid blockerUserId, Guid targetUserId, CancellationToken cancellationToken);

    Task<SocialBlockOutcome> UnblockUserAsync(Guid blockerUserId, Guid targetUserId, CancellationToken cancellationToken);

    Task<bool> IsBlockedBetweenAsync(Guid userA, Guid userB, CancellationToken cancellationToken);

    /// <summary>Hồ sơ tóm tắt của một người dùng cho popup avatar: số follow + quan hệ với viewer.</summary>
    Task<SocialProfileSummary?> GetProfileSummaryAsync(Guid viewerUserId, Guid targetUserId, CancellationToken cancellationToken);
}
