using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Social;

namespace DNTU.SkillBridge.Application.Social.Repositories;

/// <summary>Contract persistence cho mạng lưới theo dõi.</summary>
public interface ISocialRepository
{
    // ----- Follow -----
    Task<UserFollow?> GetFollowAsync(Guid followerUserId, Guid followeeUserId, CancellationToken cancellationToken);
    Task AddFollowAsync(UserFollow follow, CancellationToken cancellationToken);
    Task<bool> RemoveFollowAsync(Guid followerUserId, Guid followeeUserId, CancellationToken cancellationToken);
    Task<bool> IsFollowingAsync(Guid followerUserId, Guid followeeUserId, CancellationToken cancellationToken);
    Task<bool> IsFollowedByAsync(Guid userId, Guid followerUserId, CancellationToken cancellationToken);
    Task<int> CountFollowersAsync(Guid userId, CancellationToken cancellationToken);
    Task<int> CountFollowingAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetFolloweeIdsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SocialNetworkRow>> ListNetworkAsync(Guid userId, string direction, string? roleFilter, int skip, int take, CancellationToken cancellationToken);
    Task<int> CountNetworkAsync(Guid userId, string direction, string? roleFilter, CancellationToken cancellationToken);

    // ----- Block -----
    Task<bool> IsBlockedBetweenAsync(Guid userA, Guid userB, CancellationToken cancellationToken);
    Task AddBlockAsync(UserBlock block, CancellationToken cancellationToken);
    Task<bool> RemoveBlockAsync(Guid blockerUserId, Guid blockedUserId, CancellationToken cancellationToken);
    Task RemoveFollowsBetweenAsync(Guid userA, Guid userB, CancellationToken cancellationToken);

    // ----- Feed -----
    Task<IReadOnlyList<SocialActivityRow>> ListActivitiesAsync(IReadOnlyCollection<Guid> actorIds, int skip, int take, CancellationToken cancellationToken);
    Task<int> CountActivitiesAsync(IReadOnlyCollection<Guid> actorIds, CancellationToken cancellationToken);

    // ----- Gợi ý kết nối -----
    Task<IReadOnlyList<SocialSuggestionRow>> GetSuggestionsAsync(Guid viewerUserId, int limit, CancellationToken cancellationToken);

    // ----- Tiện ích -----
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken);
    Task<(Guid UserId, string DisplayName, string? AvatarUrl, string Role)?> GetUserBasicsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Hồ sơ đầy đủ (kèm Detail: mã SV / khoa / tên công ty) cho popup profile.</summary>
    Task<SocialUserProfileData?> GetUserProfileDataAsync(Guid userId, CancellationToken cancellationToken);
    Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
