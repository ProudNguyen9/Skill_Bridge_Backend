using Asp.Versioning;
using DNTU.SkillBridge.Application.Administration;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/cms")]
[Produces("application/json")]
public sealed class CmsController(
    IAdminGovernanceService governanceService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Đọc nội dung trang Giới thiệu (/about) từ CMS Database.
    /// Nếu lần đầu chưa có bản ghi, hệ thống tự khởi tạo nội dung hiện tại vào DB.
    /// </summary>
    [HttpGet("about")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AdminPolicyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AdminPolicyResponse>>> GetAboutContent(CancellationToken cancellationToken)
    {
        var policy = await governanceService.GetPolicyAsync(AboutCmsDefaultPayload.CategoryKey, cancellationToken);
        return policy is null ? NotFound() : Ok(new ApiResponse<AdminPolicyResponse>(policy));
    }

    /// <summary>
    /// Cập nhật nội dung trang Giới thiệu (/about) vào CMS Database (Chỉ dành cho Admin / SuperAdmin).
    /// </summary>
    [HttpPut("about")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AdminPolicyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<AdminPolicyResponse>>> UpdateAboutContent(
        [FromBody] JsonSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var policy = await governanceService.UpdatePolicyAsync(
            currentUser.UserId!.Value,
            AboutCmsDefaultPayload.CategoryKey,
            request.SettingsJson,
            cancellationToken);
        return Ok(new ApiResponse<AdminPolicyResponse>(policy));
    }

    /// <summary>
    /// Khôi phục nội dung trang Giới thiệu (/about) về bản gốc chuẩn DNTU trong Database.
    /// </summary>
    [HttpPost("about/reset")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AdminPolicyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<AdminPolicyResponse>>> ResetAboutContent(CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var policy = await governanceService.UpdatePolicyAsync(
            currentUser.UserId!.Value,
            AboutCmsDefaultPayload.CategoryKey,
            AboutCmsDefaultPayload.DefaultJson,
            cancellationToken);
        return Ok(new ApiResponse<AdminPolicyResponse>(policy));
    }

    private bool IsAdmin() =>
        currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin);
}
