using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Application.Companies;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>Company tenant endpoints (/me) plus the public, privacy-limited company directory.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies")]
[Produces("application/json")]
public sealed class CompaniesController(ICompanyService companyService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Returns the caller's own company profile, or 404 until the company is created.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompanyProfileResponse>>> GetMe(CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var company = await companyService.GetMyCompanyAsync(currentUser.UserId!.Value, cancellationToken);
        return company is null ? NotFound() : Ok(new ApiResponse<CompanyProfileResponse>(company));
    }

    /// <summary>Creates the caller's company on first save (creator becomes OWNER) or updates the existing profile.</summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CompanyProfileResponse>>> UpdateMe(
        UpdateCompanyProfileRequest request, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var (outcome, profile) = await companyService.CreateOrUpdateMyCompanyAsync(currentUser.UserId!.Value, request, cancellationToken);
        return outcome switch
        {
            CompanyUpdateOutcome.NameRequired => NameRequired(),
            CompanyUpdateOutcome.Conflict => Conflict(),
            _ => Ok(new ApiResponse<CompanyProfileResponse>(profile!))
        };
    }

    /// <summary>Lists the members of the caller's company.</summary>
    [HttpGet("me/members")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CompanyMemberResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CompanyMemberResponse>>>> GetMembers(CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var members = await companyService.GetMyMembersAsync(currentUser.UserId!.Value, cancellationToken);
        return members is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<CompanyMemberResponse>>(members));
    }

    /// <summary>Invites a member by email. Only owners and managers may invite.</summary>
    [HttpPost("me/members/invitations")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CompanyInvitationResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompanyInvitationResponse>>> Invite(
        CreateCompanyInvitationRequest request, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var invitation = await companyService.InviteMemberAsync(currentUser.UserId!.Value, request, cancellationToken);
        return invitation is null ? NotFound() : Created("", new ApiResponse<CompanyInvitationResponse>(invitation));
    }

    /// <summary>Removes a company member; the last owner cannot be removed.</summary>
    [HttpDelete("me/members/{userId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid userId, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var outcome = await companyService.RemoveMemberAsync(currentUser.UserId!.Value, userId, cancellationToken);
        return outcome switch
        {
            CompanyRemoveMemberOutcome.Removed => NoContent(),
            CompanyRemoveMemberOutcome.LastOwner => LastOwnerProblem(),
            CompanyRemoveMemberOutcome.NotMember => NotFound(),
            _ => Forbid()
        };
    }

    /// <summary>Lists the caller's verification documents.</summary>
    [HttpGet("me/documents")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CompanyDocumentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CompanyDocumentResponse>>>> GetDocuments(CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var documents = await companyService.GetMyDocumentsAsync(currentUser.UserId!.Value, cancellationToken);
        return documents is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<CompanyDocumentResponse>>(documents));
    }

    /// <summary>Registers verification document metadata (file content arrives with Task 23).</summary>
    [HttpPost("me/documents")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CompanyDocumentResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompanyDocumentResponse>>> AddDocument(
        UpsertCompanyDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        var document = await companyService.AddMyDocumentAsync(currentUser.UserId!.Value, request, cancellationToken);
        return document is null ? NotFound() : Created("", new ApiResponse<CompanyDocumentResponse>(document));
    }

    /// <summary>Deletes a document owned by the caller's company.</summary>
    [HttpDelete("me/documents/{documentId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(Guid documentId, CancellationToken cancellationToken)
    {
        if (!IsCompany())
        {
            return Forbid();
        }

        return await companyService.DeleteMyDocumentAsync(currentUser.UserId!.Value, documentId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>Public directory of verified, active companies.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<PublicCompanyResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PublicCompanyResponse>>>> ListPublic(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyCollection<PublicCompanyResponse>>(
            await companyService.ListPublicCompaniesAsync(cancellationToken)));

    /// <summary>Public company profile by slug; unverified or inactive companies answer 404.</summary>
    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PublicCompanyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PublicCompanyResponse>>> GetPublic(string slug, CancellationToken cancellationToken)
    {
        var company = await companyService.GetPublicCompanyAsync(slug, cancellationToken);
        return company is null ? NotFound() : Ok(new ApiResponse<PublicCompanyResponse>(company));
    }

    /// <summary>Public project list per company slug.</summary>
    [HttpGet("{slug}/projects")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> GetPublicProjects(string slug, CancellationToken cancellationToken)
    {
        var company = await companyService.GetPublicCompanyAsync(slug, cancellationToken);
        return company is null
            ? NotFound()
            : Ok(new ApiResponse<IReadOnlyCollection<object>>(await companyService.ListPublicCompanyProjectsAsync(slug, cancellationToken)));
    }

    private bool IsCompany() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Company);

    private ActionResult NameRequired()
    {
        ModelState.AddModelError(nameof(UpdateCompanyProfileRequest.Name), "A company name is required when creating the profile.");
        return ValidationProblem(ModelState);
    }

    private ActionResult LastOwnerProblem()
    {
        ModelState.AddModelError("members", "The last owner of a company cannot be removed.");
        return ValidationProblem(ModelState);
    }
}
