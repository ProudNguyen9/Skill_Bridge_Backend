using System.Text.Json;
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
[Route("api/v1/cms/home")]
[Produces("application/json")]
public sealed class HomeCmsController(
    IAdminGovernanceService governanceService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AdminPolicyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AdminPolicyResponse>>> GetHomeContent(CancellationToken cancellationToken)
    {
        var policy = await governanceService.GetPolicyAsync(HomeCmsDefaultPayload.CategoryKey, cancellationToken);
        policy ??= new AdminPolicyResponse(HomeCmsDefaultPayload.CategoryKey, HomeCmsDefaultPayload.DefaultJson, 0, null);
        return Ok(new ApiResponse<AdminPolicyResponse>(policy));
    }

    [HttpPut]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AdminPolicyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<AdminPolicyResponse>>> UpdateHomeContent(
        [FromBody] HomeCmsSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        if (!IsValidSettings(request.SettingsJson))
        {
            return BadRequest(new { message = "Nội dung CMS trang chủ không hợp lệ hoặc vượt quá giới hạn 250 KB." });
        }

        var policy = await governanceService.UpdatePolicyAsync(
            currentUser.UserId!.Value,
            HomeCmsDefaultPayload.CategoryKey,
            request.SettingsJson,
            cancellationToken);
        return Ok(new ApiResponse<AdminPolicyResponse>(policy));
    }

    [HttpPost("reset")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AdminPolicyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<AdminPolicyResponse>>> ResetHomeContent(CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var policy = await governanceService.UpdatePolicyAsync(
            currentUser.UserId!.Value,
            HomeCmsDefaultPayload.CategoryKey,
            HomeCmsDefaultPayload.DefaultJson,
            cancellationToken);
        return Ok(new ApiResponse<AdminPolicyResponse>(policy));
    }

    private bool IsAdmin() =>
        currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin);

    private static bool IsValidSettings(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.Length > 250_000) return false;
        try
        {
            using var document = JsonDocument.Parse(settingsJson);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("header", out _)
                && root.TryGetProperty("hero", out _)
                && root.TryGetProperty("projects", out _)
                && root.TryGetProperty("workspace", out _)
                && root.TryGetProperty("passport", out _)
                && root.TryGetProperty("audience", out _)
                && root.TryGetProperty("footer", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public sealed record HomeCmsSettingsRequest(string SettingsJson);
