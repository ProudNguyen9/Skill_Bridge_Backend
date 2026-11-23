using System.Text.Json;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Application.Social.Repositories;
using DNTU.SkillBridge.Domain.Social;

namespace DNTU.SkillBridge.Application.Social.Services;

/// <summary>Nghiệp vụ mạng lưới theo dõi: Follow/Unfollow, danh bạ, bảng tin, gợi ý kết nối, chặn.</summary>
public sealed class SocialService(
    ISocialRepository socialRepository,
    INotificationWriter? notificationWriter = null) : ISocialService
{
    public async Task<(SocialFollowOutcome, SocialFollowStatus?, string?)> FollowAsync(Guid followerUserId, Guid targetUserId, CancellationToken cancellationToken)
    {
        if (followerUserId == targetUserId)
        {
            return (SocialFollowOutcome.SelfFollow, null, "Không thể theo dõi chính mình.");
        }

        if (!await socialRepository.UserExistsAsync(targetUserId, cancellationToken))
        {
            return (SocialFollowOutcome.TargetNotFound, null, "Không tìm thấy người dùng cần theo dõi.");
        }

        if (await socialRepository.IsAdminAsync(targetUserId, cancellationToken))
        {
            return (SocialFollowOutcome.TargetNotFound, null, "Không thể theo dõi tài khoản quản trị viên.");
        }

        if (await socialRepository.IsBlockedBetweenAsync(followerUserId, targetUserId, cancellationToken))
        {
            return (SocialFollowOutcome.Blocked, null, "Không thể theo dõi người dùng này.");
        }

        var existing = await socialRepository.GetFollowAsync(followerUserId, targetUserId, cancellationToken);
        if (existing is null)
        {
            await socialRepository.AddFollowAsync(new UserFollow(followerUserId, targetUserId), cancellationToken);
            var followerName = await socialRepository.GetUserDisplayNameAsync(followerUserId, cancellationToken) ?? "Ai đó";
            notificationWriter?.Add(new NotificationMessage(
                targetUserId,
                "SOCIAL_FOLLOW",
                $"{followerName} đã theo dõi bạn",
                JsonSerializer.Serialize(new { userId = followerUserId })));
            await socialRepository.SaveAsync(cancellationToken);
        }

        var status = await BuildFollowStatusAsync(followerUserId, targetUserId, cancellationToken);
        return (SocialFollowOutcome.Success, status, null);
    }

    public async Task<SocialFollowStatus> UnfollowAsync(Guid followerUserId, Guid targetUserId, CancellationToken cancellationToken)
    {
        await socialRepository.RemoveFollowAsync(followerUserId, targetUserId, cancellationToken);
        await socialRepository.SaveAsync(cancellationToken);
        return await BuildFollowStatusAsync(followerUserId, targetUserId, cancellationToken);
    }

    public Task<SocialFollowStatus> GetFollowStatusAsync(Guid viewerUserId, Guid targetUserId, CancellationToken cancellationToken) =>
        BuildFollowStatusAsync(viewerUserId, targetUserId, cancellationToken);

    public async Task<PagedResponse<SocialNetworkUser>> GetNetworkAsync(Guid viewerUserId, Guid userId, string direction, string? roleFilter, int page, int pageSize, CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var rows = await socialRepository.ListNetworkAsync(userId, direction, roleFilter, (page - 1) * pageSize, pageSize, cancellationToken);
        var total = await socialRepository.CountNetworkAsync(userId, direction, roleFilter, cancellationToken);
        var items = new List<SocialNetworkUser>(rows.Count);
        foreach (var row in rows)
        {
            var viewerFollows = row.UserId == viewerUserId
                ? false
                : await socialRepository.IsFollowingAsync(viewerUserId, row.UserId, cancellationToken);
            items.Add(new SocialNetworkUser(
                row.UserId,
                row.DisplayName,
                row.AvatarUrl,
                row.Role,
                row.Detail,
                viewerFollows,
                await socialRepository.IsFollowedByAsync(viewerUserId, row.UserId, cancellationToken),
                row.FollowedAt));
        }

        return new PagedResponse<SocialNetworkUser>(items, PageMetadata.Create(page, pageSize, total));
    }

    public async Task<PagedResponse<SocialFeedItem>> GetFeedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var followeeIds = await socialRepository.GetFolloweeIdsAsync(userId, cancellationToken);
        if (followeeIds.Count == 0)
        {
            return new PagedResponse<SocialFeedItem>([], PageMetadata.Create(page, pageSize, 0));
        }

        var rows = await socialRepository.ListActivitiesAsync(followeeIds, (page - 1) * pageSize, pageSize, cancellationToken);
        var total = await socialRepository.CountActivitiesAsync(followeeIds, cancellationToken);
        var items = rows.Select(row => new SocialFeedItem(
            row.Id,
            row.ActivityType.ToString(),
            row.Title,
            row.Summary,
            row.LinkUrl,
            row.TargetEntityId,
            new SocialActorSummary(row.ActorUserId, row.ActorDisplayName, row.ActorAvatarUrl, row.ActorRole),
            row.CreatedAt)).ToList();
        return new PagedResponse<SocialFeedItem>(items, PageMetadata.Create(page, pageSize, total));
    }

    public async Task<IReadOnlyList<SocialSuggestionUser>> GetSuggestionsAsync(Guid userId, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 20);
        var rows = await socialRepository.GetSuggestionsAsync(userId, limit, cancellationToken);
        var suggestions = new List<SocialSuggestionUser>(rows.Count);
        foreach (var row in rows)
        {
            if (await socialRepository.IsBlockedBetweenAsync(userId, row.UserId, cancellationToken)) continue;
            suggestions.Add(new SocialSuggestionUser(row.UserId, row.DisplayName, row.AvatarUrl, row.Role, row.Reason, row.SharedCount, row.ViewerFollows));
        }

        return suggestions.Take(limit).ToList();
    }

    public async Task<(SocialBlockOutcome, string?)> BlockUserAsync(Guid blockerUserId, Guid targetUserId, CancellationToken cancellationToken)
    {
        if (blockerUserId == targetUserId)
        {
            return (SocialBlockOutcome.SelfBlock, "Không thể tự chặn chính mình.");
        }

        if (!await socialRepository.UserExistsAsync(targetUserId, cancellationToken))
        {
            return (SocialBlockOutcome.TargetNotFound, "Không tìm thấy người dùng cần chặn.");
        }

        var exists = await socialRepository.IsBlockedBetweenAsync(blockerUserId, targetUserId, cancellationToken);
        if (!exists)
        {
            await socialRepository.AddBlockAsync(new UserBlock(blockerUserId, targetUserId), cancellationToken);
            // Chặn hủy Follow hai chiều.
            await socialRepository.RemoveFollowsBetweenAsync(blockerUserId, targetUserId, cancellationToken);
            await socialRepository.SaveAsync(cancellationToken);
        }

        return (SocialBlockOutcome.Success, null);
    }

    public async Task<SocialBlockOutcome> UnblockUserAsync(Guid blockerUserId, Guid targetUserId, CancellationToken cancellationToken)
    {
        await socialRepository.RemoveBlockAsync(blockerUserId, targetUserId, cancellationToken);
        await socialRepository.SaveAsync(cancellationToken);
        return SocialBlockOutcome.Success;
    }

    public Task<bool> IsBlockedBetweenAsync(Guid userA, Guid userB, CancellationToken cancellationToken) =>
        socialRepository.IsBlockedBetweenAsync(userA, userB, cancellationToken);

    /// <inheritdoc />
    public async Task<SocialProfileSummary?> GetProfileSummaryAsync(Guid viewerUserId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var profile = await socialRepository.GetUserProfileDataAsync(targetUserId, cancellationToken);
        if (profile is null) return null;

        var isFollowing = await socialRepository.IsFollowingAsync(viewerUserId, targetUserId, cancellationToken);
        var isFollowedBy = await socialRepository.IsFollowedByAsync(viewerUserId, targetUserId, cancellationToken);

        return new SocialProfileSummary(
            profile.UserId,
            profile.DisplayName,
            profile.AvatarUrl,
            profile.Role,
            profile.Detail,
            await socialRepository.CountFollowersAsync(targetUserId, cancellationToken),
            await socialRepository.CountFollowingAsync(targetUserId, cancellationToken),
            isFollowing,
            isFollowedBy,
            isFollowing && isFollowedBy);
    }

    private async Task<SocialFollowStatus> BuildFollowStatusAsync(Guid viewerUserId, Guid targetUserId, CancellationToken cancellationToken) =>
        new(
            targetUserId,
            await socialRepository.IsFollowingAsync(viewerUserId, targetUserId, cancellationToken),
            await socialRepository.IsFollowedByAsync(viewerUserId, targetUserId, cancellationToken),
            await socialRepository.IsFollowingAsync(viewerUserId, targetUserId, cancellationToken) && await socialRepository.IsFollowedByAsync(viewerUserId, targetUserId, cancellationToken),
            await socialRepository.CountFollowersAsync(targetUserId, cancellationToken),
            await socialRepository.CountFollowingAsync(targetUserId, cancellationToken));
}
