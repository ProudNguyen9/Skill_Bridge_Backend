using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Application.Students;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>Authenticated student profile endpoints plus the privacy-limited public profile read.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/students")]
[Produces("application/json")]
public sealed class StudentsController(IStudentService studentService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Returns the caller's own student profile, creating it with default privacy settings on first access.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StudentProfileResponse>>> GetMe(CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        return Ok(new ApiResponse<StudentProfileResponse>(
            await studentService.GetMyProfileAsync(currentUser.UserId!.Value, cancellationToken)));
    }

    /// <summary>Updates the caller's own profile; the student id is always taken from the session, never from the payload.</summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StudentProfileResponse>>> UpdateMe(
        UpdateStudentProfileRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        var updated = await studentService.UpdateMyProfileAsync(currentUser.UserId!.Value, request, cancellationToken);
        return updated is null
            ? InvalidCatalogReference()
            : Ok(new ApiResponse<StudentProfileResponse>(updated));
    }

    /// <summary>Updates the caller's own privacy settings, controlling what the public profile exposes.</summary>
    [HttpPut("me/privacy")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<StudentPrivacyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StudentPrivacyResponse>>> UpdatePrivacy(
        UpdateStudentPrivacyRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        return Ok(new ApiResponse<StudentPrivacyResponse>(
            await studentService.UpdateMyPrivacyAsync(currentUser.UserId!.Value, request, cancellationToken)));
    }

    /// <summary>Lists the caller's self-declared skills. Verified skills live elsewhere and are never reported here.</summary>
    [HttpGet("me/skills")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<DeclaredSkillResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DeclaredSkillResponse>>>> GetSkills(CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        return Ok(new ApiResponse<IReadOnlyCollection<DeclaredSkillResponse>>(
            await studentService.GetMySkillsAsync(currentUser.UserId!.Value, cancellationToken)));
    }

    /// <summary>Replaces the caller's self-declared skill set; every skill must be an active catalog skill.</summary>
    [HttpPut("me/skills")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<DeclaredSkillResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DeclaredSkillResponse>>>> ReplaceSkills(
        ReplaceDeclaredSkillsRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        if (!await studentService.ReplaceMySkillsAsync(currentUser.UserId!.Value, request.Skills, cancellationToken))
        {
            return InvalidCatalogReference();
        }

        return Ok(new ApiResponse<IReadOnlyCollection<DeclaredSkillResponse>>(
            await studentService.GetMySkillsAsync(currentUser.UserId!.Value, cancellationToken)));
    }

    /// <summary>Lists the caller's own certificates, newest first.</summary>
    [HttpGet("me/certificates")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CertificateResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CertificateResponse>>>> GetCertificates(CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        return Ok(new ApiResponse<IReadOnlyCollection<CertificateResponse>>(
            await studentService.GetMyCertificatesAsync(currentUser.UserId!.Value, cancellationToken)));
    }

    /// <summary>Creates a certificate owned by the caller.</summary>
    [HttpPost("me/certificates")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CertificateResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<CertificateResponse>>> AddCertificate(
        UpsertCertificateRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        var certificate = await studentService.AddMyCertificateAsync(currentUser.UserId!.Value, request, cancellationToken);
        return Created("", new ApiResponse<CertificateResponse>(certificate));
    }

    /// <summary>Updates a certificate owned by the caller; other students' certificates are not visible.</summary>
    [HttpPut("me/certificates/{certificateId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CertificateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CertificateResponse>>> UpdateCertificate(
        Guid certificateId, UpsertCertificateRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        var certificate = await studentService.UpdateMyCertificateAsync(currentUser.UserId!.Value, certificateId, request, cancellationToken);
        return certificate is null
            ? NotFound()
            : Ok(new ApiResponse<CertificateResponse>(certificate));
    }

    /// <summary>Deletes a certificate owned by the caller; other students' certificates are not visible.</summary>
    [HttpDelete("me/certificates/{certificateId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCertificate(Guid certificateId, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return Forbid();
        }

        return await studentService.DeleteMyCertificateAsync(currentUser.UserId!.Value, certificateId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>
    /// Public profile read. Answers 404 when the profile does not exist or the student keeps it private;
    /// otherwise returns the privacy-filtered projection without contact info, CV, or hidden collections.
    /// </summary>
    [HttpGet("{studentId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PublicStudentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PublicStudentResponse>>> GetPublic(Guid studentId, CancellationToken cancellationToken)
    {
        var profile = await studentService.GetPublicStudentAsync(studentId, cancellationToken);
        return profile is null
            ? NotFound()
            : Ok(new ApiResponse<PublicStudentResponse>(profile));
    }

    private bool IsStudent() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Student);

    private ActionResult InvalidCatalogReference()
    {
        ModelState.AddModelError("catalog", "Major, faculty, or skill references must be active catalog values.");
        return ValidationProblem(ModelState);
    }
}
