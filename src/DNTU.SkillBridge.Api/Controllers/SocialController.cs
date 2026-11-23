using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Application.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>REST surface của mạng lưới theo dõi: Follow, danh bạ, bảng tin, gợi ý, chặn.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/social")]
[Authorize]
[Produces("application/json")]
public sealed class SocialController(ISocialService socialService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Theo dõi một người dùng / doanh nghiệp / giảng viên.</summary>
    [HttpPost("follows/{targetUserId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SocialFollowStatus>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SocialFollowStatus>>> Follow(Guid targetUserId, CancellationToken cancellationToken)
    {
        var (outcome, status, error) = await socialService.FollowAsync(UserId, targetUserId, cancellationToken);
        return outcome switch
        {
            SocialFollowOutcome.Success => Ok(new ApiResponse<SocialFollowStatus>(status!)),
            SocialFollowOutcome.SelfFollow => Problem(title: "SOCIAL_SELF_FOLLOW", detail: error, statusCode: StatusCodes.Status400BadRequest),
            SocialFollowOutcome.Blocked => Problem(title: "SOCIAL_BLOCKED", detail: error, statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(title: "SOCIAL_TARGET_NOT_FOUND", detail: error, statusCode: StatusCodes.Status404NotFound),
        };
    }

    /// <summary>Bỏ theo dõi.</summary>
    [HttpDelete("follows/{targetUserId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SocialFollowStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SocialFollowStatus>>> Unfollow(Guid targetUserId, CancellationToken cancellationToken)
    {
        var status = await socialService.UnfollowAsync(UserId, targetUserId, cancellationToken);
        return Ok(new ApiResponse<SocialFollowStatus>(status));
    }

    /// <summary>Trạng thái theo dõi giữa viewer và người dùng đích (kèm số lượng).</summary>
    [HttpGet("users/{targetUserId:guid}/follow-status")]
    [ProducesResponseType(typeof(ApiResponse<SocialFollowStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SocialFollowStatus>>> GetFollowStatus(Guid targetUserId, CancellationToken cancellationToken)
    {
        var status = await socialService.GetFollowStatusAsync(UserId, targetUserId, cancellationToken);
        return Ok(new ApiResponse<SocialFollowStatus>(status));
    }

    /// <summary>Danh sách Người theo dõi / Đang theo dõi (lọc theo vai trò, phân trang).</summary>
    [HttpGet("users/{userId:guid}/network")]
    [ProducesResponseType(typeof(PagedResponse<SocialNetworkUser>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SocialNetworkUser>>> GetNetwork(
        Guid userId,
        [FromQuery] string? direction,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await socialService.GetNetworkAsync(UserId, userId, direction ?? "FOLLOWERS", role, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>Hồ sơ tóm tắt của một người dùng (popup avatar): số follow + quan hệ với viewer.</summary>
    [HttpGet("users/{targetUserId:guid}/profile")]
    [ProducesResponseType(typeof(ApiResponse<DNTU.SkillBridge.Application.Social.SocialProfileSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DNTU.SkillBridge.Application.Social.SocialProfileSummary>>> GetProfile(
        Guid targetUserId, CancellationToken cancellationToken)
    {
        var profile = await socialService.GetProfileSummaryAsync(UserId, targetUserId, cancellationToken);
        return profile is null
            ? Problem(title: "SOCIAL_PROFILE_NOT_FOUND", statusCode: StatusCodes.Status404NotFound)
            : Ok(new ApiResponse<DNTU.SkillBridge.Application.Social.SocialProfileSummary>(profile));
    }

    /// <summary>Bảng tin cập nhật từ những đối tượng đang theo dõi.</summary>
    [HttpGet("feed")]
    [ProducesResponseType(typeof(PagedResponse<SocialFeedItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SocialFeedItem>>> GetFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        var result = await socialService.GetFeedAsync(UserId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>Gợi ý kết nối dựa trên chuyên ngành / kỹ năng trùng khớp.</summary>
    [HttpGet("suggestions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SocialSuggestionUser>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SocialSuggestionUser>>>> GetSuggestions(
        [FromQuery] int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var suggestions = await socialService.GetSuggestionsAsync(UserId, limit, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<SocialSuggestionUser>>(suggestions));
    }

    /// <summary>Chặn người dùng: ngừng nhận tin nhắn + hủy Follow hai chiều.</summary>
    [HttpPost("blocks/{targetUserId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> BlockUser(Guid targetUserId, CancellationToken cancellationToken)
    {
        var (outcome, error) = await socialService.BlockUserAsync(UserId, targetUserId, cancellationToken);
        return outcome switch
        {
            SocialBlockOutcome.Success => Ok(new ApiResponse<bool>(true)),
            SocialBlockOutcome.SelfBlock => Problem(title: "SOCIAL_SELF_BLOCK", detail: error, statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(title: "SOCIAL_TARGET_NOT_FOUND", detail: error, statusCode: StatusCodes.Status404NotFound),
        };
    }

    /// <summary>Bỏ chặn.</summary>
    [HttpDelete("blocks/{targetUserId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> UnblockUser(Guid targetUserId, CancellationToken cancellationToken)
    {
        var outcome = await socialService.UnblockUserAsync(UserId, targetUserId, cancellationToken);
        return Ok(new ApiResponse<bool>(outcome == SocialBlockOutcome.Success));
    }

    private Guid UserId => currentUser.UserId!.Value;
}
