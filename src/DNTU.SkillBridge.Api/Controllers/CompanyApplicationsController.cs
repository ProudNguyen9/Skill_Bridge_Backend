using Asp.Versioning;
using DNTU.SkillBridge.Application.Applications;
using DNTU.SkillBridge.Application.Companies;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Applications;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/company/applications")]
[Authorize]
[Produces("application/json")]
public sealed class CompanyApplicationsController(IApplicationService applicationService, ICompanyService companyService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CompanyApplicationResponse>>> List([FromQuery] CompanyApplicationListQuery query, CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdAsync(cancellationToken);
        return companyId is null ? Forbid() : Ok(await applicationService.ListCompanyApplicationsAsync(companyId.Value, query, cancellationToken));
    }

    [HttpGet("{applicationId:guid}")]
    public async Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> Get(Guid applicationId, CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdAsync(cancellationToken);
        if (companyId is null) return Forbid();
        var application = await applicationService.GetCompanyApplicationAsync(companyId.Value, applicationId, cancellationToken);
        return application is null ? NotFound() : Ok(new ApiResponse<CompanyApplicationResponse>(application));
    }

    /// <summary>Places a pending application on the company shortlist.</summary>
    [HttpPost("{applicationId:guid}/shortlist")]
    [ProducesResponseType(typeof(ApiResponse<CompanyApplicationResponse>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> Shortlist(Guid applicationId, [FromBody] ApplicationDecisionRequest request, CancellationToken cancellationToken) => Decide(applicationId, ApplicationStatus.SHORTLISTED, request.Reason, cancellationToken);

    /// <summary>Rejects an active application and retains the optional decision reason.</summary>
    [HttpPost("{applicationId:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<CompanyApplicationResponse>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> Reject(Guid applicationId, [FromBody] ApplicationDecisionRequest request, CancellationToken cancellationToken) => Decide(applicationId, ApplicationStatus.REJECTED, request.Reason, cancellationToken);

    /// <summary>Atomically accepts an application and creates its pending student commitment.</summary>
    [HttpPost("{applicationId:guid}/accept")]
    [ProducesResponseType(typeof(ApiResponse<CompanyApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> Accept(Guid applicationId, [FromBody] ApplicationDecisionRequest request, CancellationToken cancellationToken) => Decide(applicationId, ApplicationStatus.ACCEPTED, request.Reason, cancellationToken);

    private async Task<ActionResult<ApiResponse<CompanyApplicationResponse>>> Decide(Guid applicationId, ApplicationStatus status, string? reason, CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdAsync(cancellationToken);
        if (companyId is null) return Forbid();
        var (outcome, application) = await applicationService.DecideCompanyApplicationAsync(companyId.Value, currentUser.UserId!.Value, applicationId, status, reason, cancellationToken);
        return outcome switch
        {
            ApplicationWithdrawOutcome.Withdrawn when application is not null => Ok(new ApiResponse<CompanyApplicationResponse>(application)),
            ApplicationWithdrawOutcome.NotFound => NotFound(),
            _ => Conflict()
        };
    }

    private async Task<Guid?> CompanyIdAsync(CancellationToken cancellationToken) =>
        !currentUser.IsAuthenticated || !currentUser.Roles.Contains(RoleNames.Company)
            ? null
            : await companyService.ResolveCompanyIdAsync(currentUser.UserId!.Value, cancellationToken);
}
