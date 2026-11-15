using Asp.Versioning;
using DNTU.SkillBridge.Application.Catalog;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Catalog;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>
/// Quản trị danh mục hệ thống (kỹ năng, ngành nghề, khoa, bộ môn) — chỉ ADMIN/SUPER_ADMIN.
/// Bản ghi bị "xóa" thực chất bị vô hiệu hóa ( IsActive=false ) để giữ toàn vẹn dữ liệu lịch sử.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/admin/catalog")]
[Authorize]
[Produces("application/json")]
public sealed class AdminCatalogController(IAdminCatalogService catalogService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("skills")]
    [ProducesResponseType(typeof(ApiResponse<SkillResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<SkillResponse>>> CreateSkill(
        [FromBody] CreateSkillRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        if (!IsCategoryValid(request.Category)) return BadRequest();

        var result = await catalogService.CreateSkillAsync(currentUser.UserId!.Value, request, cancellationToken);
        return result.Status == AdminCatalogWriteStatus.Duplicate ? Conflict() : Created($"/api/v1/catalog/skills", new ApiResponse<SkillResponse>(result.Item!));
    }

    [HttpPut("skills/{skillId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SkillResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<SkillResponse>>> UpdateSkill(
        Guid skillId, [FromBody] UpdateSkillRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        if (!IsCategoryValid(request.Category)) return BadRequest();

        var result = await catalogService.UpdateSkillAsync(currentUser.UserId!.Value, skillId, request, cancellationToken);
        return Respond(result);
    }

    /// <summary>Vô hiệu hóa (soft-delete) một kỹ năng.</summary>
    [HttpDelete("skills/{skillId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SkillResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<SkillResponse>>> DeactivateSkill(Guid skillId, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var result = await catalogService.SetSkillActiveAsync(currentUser.UserId!.Value, skillId, isActive: false, cancellationToken);
        return Respond(result);
    }

    [HttpPost("skills/{skillId:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<SkillResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<SkillResponse>>> ActivateSkill(Guid skillId, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var result = await catalogService.SetSkillActiveAsync(currentUser.UserId!.Value, skillId, isActive: true, cancellationToken);
        return Respond(result);
    }

    [HttpPost("industries")]
    [ProducesResponseType(typeof(ApiResponse<IndustryResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IndustryResponse>>> CreateIndustry(
        [FromBody] CreateIndustryRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var result = await catalogService.CreateIndustryAsync(currentUser.UserId!.Value, request, cancellationToken);
        return result.Status == AdminCatalogWriteStatus.Duplicate
            ? Conflict()
            : Created($"/api/v1/catalog/industries", new ApiResponse<IndustryResponse>(result.Item!));
    }

    [HttpDelete("industries/{industryId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IndustryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IndustryResponse>>> DeactivateIndustry(Guid industryId, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var result = await catalogService.SetIndustryActiveAsync(currentUser.UserId!.Value, industryId, isActive: false, cancellationToken);
        return Respond(result);
    }

    [HttpPost("faculties")]
    [ProducesResponseType(typeof(ApiResponse<FacultyResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<FacultyResponse>>> CreateFaculty(
        [FromBody] CreateFacultyRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var result = await catalogService.CreateFacultyAsync(currentUser.UserId!.Value, request, cancellationToken);
        return result.Status == AdminCatalogWriteStatus.Duplicate
            ? Conflict()
            : Created($"/api/v1/catalog/faculties", new ApiResponse<FacultyResponse>(result.Item!));
    }

    [HttpDelete("faculties/{facultyId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FacultyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<FacultyResponse>>> DeactivateFaculty(Guid facultyId, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var result = await catalogService.SetFacultyActiveAsync(currentUser.UserId!.Value, facultyId, isActive: false, cancellationToken);
        return Respond(result);
    }

    [HttpPost("majors")]
    [ProducesResponseType(typeof(ApiResponse<MajorResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<MajorResponse>>> CreateMajor(
        [FromBody] CreateMajorRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var result = await catalogService.CreateMajorAsync(currentUser.UserId!.Value, request, cancellationToken);
        return result.Status switch
        {
            AdminCatalogWriteStatus.FacultyMissing => BadRequest(),
            AdminCatalogWriteStatus.Duplicate => Conflict(),
            _ => Created($"/api/v1/catalog/majors", new ApiResponse<MajorResponse>(result.Item!))
        };
    }

    [HttpDelete("majors/{majorId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MajorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<MajorResponse>>> DeactivateMajor(Guid majorId, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var result = await catalogService.SetMajorActiveAsync(currentUser.UserId!.Value, majorId, isActive: false, cancellationToken);
        return Respond(result);
    }

    private ActionResult Respond<T>(AdminCatalogWriteResult<T> result) where T : class =>
        result.Status switch
        {
            AdminCatalogWriteStatus.NotFound => NotFound(),
            AdminCatalogWriteStatus.Duplicate => Conflict(),
            _ => Ok(new ApiResponse<T>(result.Item!))
        };

    private bool IsAdmin() =>
        currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin);

    private static bool IsCategoryValid(string category) =>
        Enum.TryParse<SkillCategory>(category.Trim(), ignoreCase: true, out _);
}
