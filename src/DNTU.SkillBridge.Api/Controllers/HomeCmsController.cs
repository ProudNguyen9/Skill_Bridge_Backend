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

    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<HomeProjectCategoryResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<HomeProjectCategoryResponse>>>> GetProjectCategories(CancellationToken cancellationToken)
    {
        var policy = await governanceService.GetPolicyAsync(HomeCmsDefaultPayload.CategoryKey, cancellationToken);
        var settingsJson = policy?.SettingsJson ?? HomeCmsDefaultPayload.DefaultJson;
        using var document = JsonDocument.Parse(settingsJson);
        var categories = new List<HomeProjectCategoryResponse>();
        if (document.RootElement.TryGetProperty("projects", out var projects) && projects.ValueKind == JsonValueKind.Object &&
            projects.TryGetProperty("categories", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var id = item.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.String ? idValue.GetString() : null;
                var label = item.TryGetProperty("label", out var labelValue) && labelValue.ValueKind == JsonValueKind.String ? labelValue.GetString() : null;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label)) continue;

                var hasKeywords = item.TryGetProperty("keywords", out var keywordValues) && keywordValues.ValueKind == JsonValueKind.Array;
                var keywords = hasKeywords
                    ? keywordValues.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()!).ToArray()
                    : id switch
                    {
                        "web" => new[] { "data", "iot", "ai", "machine learning", "analytics", "sensor", "dữ liệu", "phân tích" },
                        "data" => new[] { "data", "iot", "ai", "machine learning", "analytics", "sensor", "dữ liệu", "phân tích" },
                        _ => Array.Empty<string>()
                    };
                var matchMode = item.TryGetProperty("matchMode", out var matchModeValue) && matchModeValue.ValueKind == JsonValueKind.String
                    ? matchModeValue.GetString()
                    : id == "web" ? "exclude" : "include";
                var isActive = id == "all" || !item.TryGetProperty("isActive", out var activeValue) || activeValue.ValueKind != JsonValueKind.False;
                var sortOrder = item.TryGetProperty("sortOrder", out var orderValue) && orderValue.TryGetInt32(out var order) ? order : index;
                categories.Add(new HomeProjectCategoryResponse(id, label, keywords, matchMode == "exclude" ? "exclude" : "include", isActive, sortOrder));
                index++;
            }
        }

        if (categories.All(category => category.Id != "all"))
        {
            categories.Add(new HomeProjectCategoryResponse("all", "Tất cả bài toán", [], "include", true, 0));
        }

        return Ok(new ApiResponse<IReadOnlyCollection<HomeProjectCategoryResponse>>(
            categories.OrderBy(category => category.SortOrder).ThenBy(category => category.Label).ToArray()));
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
                && root.TryGetProperty("projects", out var projects)
                && IsValidProjectCategories(projects)
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

    private static bool IsValidProjectCategories(JsonElement projects)
    {
        if (projects.ValueKind != JsonValueKind.Object ||
            !projects.TryGetProperty("categories", out var categories) ||
            categories.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasAllCategory = false;
        foreach (var category in categories.EnumerateArray())
        {
            if (category.ValueKind != JsonValueKind.Object ||
                !category.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String ||
                !category.TryGetProperty("label", out var labelElement) || labelElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var id = idElement.GetString()?.Trim();
            var label = labelElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label) || !ids.Add(id)) return false;
            hasAllCategory |= string.Equals(id, "all", StringComparison.OrdinalIgnoreCase);

            if (category.TryGetProperty("keywords", out var keywords) &&
                (keywords.ValueKind != JsonValueKind.Array || keywords.EnumerateArray().Any(keyword => keyword.ValueKind != JsonValueKind.String)))
            {
                return false;
            }
            if (category.TryGetProperty("matchMode", out var matchMode) &&
                (matchMode.ValueKind != JsonValueKind.String || matchMode.GetString() is not ("include" or "exclude"))) return false;
            if (category.TryGetProperty("isActive", out var active) && active.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
            if (category.TryGetProperty("sortOrder", out var sortOrder) && !sortOrder.TryGetInt32(out _)) return false;
        }

        return hasAllCategory;
    }
}

public sealed record HomeCmsSettingsRequest(string SettingsJson);
public sealed record HomeProjectCategoryResponse(string Id, string Label, IReadOnlyCollection<string> Keywords, string MatchMode, bool IsActive, int SortOrder);
