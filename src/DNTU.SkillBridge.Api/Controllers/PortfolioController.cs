using Asp.Versioning;
using DNTU.SkillBridge.Api.Contracts;
using DNTU.SkillBridge.Api.Students;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
[Produces("application/json")]
public sealed class PortfolioController(PortfolioService portfolioService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("students/me/verified-skills")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<VerifiedSkillResponse>>>> MyVerifiedSkills(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var passport = await portfolioService.GetPassportAsync(currentUser.StudentId!.Value, publicOnly: false, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyCollection<VerifiedSkillResponse>>(passport!.VerifiedSkills));
    }

    [HttpGet("students/{studentId:guid}/verified-skills")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<VerifiedSkillResponse>>>> PublicVerifiedSkills(Guid studentId, CancellationToken cancellationToken)
    {
        var passport = await portfolioService.GetPassportAsync(studentId, publicOnly: true, cancellationToken);
        return passport is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<VerifiedSkillResponse>>(passport.VerifiedSkills));
    }

    [HttpPost("lecturer/students/{studentId:guid}/skills/{skillId:guid}/verify")]
    public async Task<ActionResult<ApiResponse<VerifiedSkillResponse>>> Verify(Guid studentId, Guid skillId, VerifySkillRequest request, CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        var skill = await portfolioService.VerifySkillAsync(UserId, studentId, skillId, request, cancellationToken);
        return skill is null ? Conflict() : Ok(new ApiResponse<VerifiedSkillResponse>(skill));
    }

    [HttpPost("lecturer/students/{studentId:guid}/skills/{skillId:guid}/revoke-verification")]
    public async Task<ActionResult<ApiResponse<VerifiedSkillResponse>>> Revoke(Guid studentId, Guid skillId, [FromQuery] Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        var skill = await portfolioService.RevokeSkillAsync(UserId, studentId, skillId, projectId, cancellationToken);
        return skill is null ? NotFound() : Ok(new ApiResponse<VerifiedSkillResponse>(skill));
    }

    [HttpGet("students/me/portfolio")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PortfolioEntryResponse>>>> MyPortfolio(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var passport = await portfolioService.GetPassportAsync(currentUser.StudentId!.Value, publicOnly: false, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyCollection<PortfolioEntryResponse>>(passport!.Portfolio));
    }

    [HttpPut("students/me/portfolio/{projectId:guid}")]
    public async Task<ActionResult<ApiResponse<PortfolioEntryResponse>>> UpsertPortfolio(Guid projectId, UpsertPortfolioEntryRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var entry = await portfolioService.UpsertPortfolioEntryAsync(UserId, projectId, request, cancellationToken);
        return entry is null ? Conflict() : Ok(new ApiResponse<PortfolioEntryResponse>(entry));
    }

    [HttpPost("students/me/portfolio/{projectId:guid}/publish")]
    public async Task<ActionResult<ApiResponse<PortfolioEntryResponse>>> PublishPortfolio(Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var entry = await portfolioService.SetPublishedAsync(UserId, projectId, published: true, cancellationToken);
        return entry is null ? Conflict() : Ok(new ApiResponse<PortfolioEntryResponse>(entry));
    }

    [HttpPost("students/me/portfolio/{projectId:guid}/unpublish")]
    public async Task<ActionResult<ApiResponse<PortfolioEntryResponse>>> UnpublishPortfolio(Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var entry = await portfolioService.SetPublishedAsync(UserId, projectId, published: false, cancellationToken);
        return entry is null ? Conflict() : Ok(new ApiResponse<PortfolioEntryResponse>(entry));
    }

    [HttpGet("students/me/portfolio/preview")]
    public Task<ActionResult<ApiResponse<SkillPassportResponse>>> Preview(CancellationToken cancellationToken) => MyPassport(cancellationToken);

    [HttpGet("students/me/skill-passport")]
    public async Task<ActionResult<ApiResponse<SkillPassportResponse>>> MyPassport(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var passport = await portfolioService.GetPassportAsync(currentUser.StudentId!.Value, publicOnly: false, cancellationToken);
        return Ok(new ApiResponse<SkillPassportResponse>(passport!));
    }

    [HttpGet("students/{studentId:guid}/portfolio")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PortfolioEntryResponse>>>> PublicPortfolio(Guid studentId, CancellationToken cancellationToken)
    {
        var passport = await portfolioService.GetPassportAsync(studentId, publicOnly: true, cancellationToken);
        return passport is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<PortfolioEntryResponse>>(passport.Portfolio));
    }

    [HttpGet("students/{studentId:guid}/skill-passport")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<SkillPassportResponse>>> PublicPassport(Guid studentId, CancellationToken cancellationToken)
    {
        var passport = await portfolioService.GetPassportAsync(studentId, publicOnly: true, cancellationToken);
        return passport is null ? NotFound() : Ok(new ApiResponse<SkillPassportResponse>(passport));
    }

    [HttpGet("students/{studentId:guid}/skill-passport/evidence")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<SkillPassportResponse>>> PublicPassportEvidence(Guid studentId, CancellationToken cancellationToken) =>
        PublicPassport(studentId, cancellationToken);

    private Guid UserId => currentUser.UserId!.Value;
    private bool IsStudent() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Student) && currentUser.StudentId.HasValue;
    private bool IsLecturer() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Lecturer);
}
