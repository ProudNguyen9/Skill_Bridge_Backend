using Asp.Versioning;
using DNTU.SkillBridge.Application.Administration;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Api.Security;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>
/// Duyệt hồ sơ giảng viên tự đăng ký (lecturers.approve) và xác minh công ty đối tác (companies.verify).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/admin")]
[Authorize]
[Produces("application/json")]
public sealed class AdminVerificationsController(IAdminVerificationService verificationService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("lecturers")]
    [ProducesResponseType(typeof(IReadOnlyCollection<AdminLecturerReviewItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<AdminLecturerReviewItem>>> ListLecturers(
        [FromQuery] string? status, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.LecturersApprove)) return Forbid();

        return Ok(new ApiResponse<IReadOnlyCollection<AdminLecturerReviewItem>>(await verificationService.ListLecturersAsync(status, cancellationToken)));
    }

    [HttpPost("lecturers/{lecturerId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApproveLecturer(
        Guid lecturerId, [FromBody] AdminVerificationDecisionRequest? request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.LecturersApprove)) return Forbid();

        var outcome = await verificationService.DecideLecturerAsync(
            currentUser.UserId!.Value, lecturerId, approve: true, request?.Note, cancellationToken);
        return outcome switch
        {
            LecturerReviewOutcome.NotFound => NotFound(),
            LecturerReviewOutcome.AlreadyProcessed => Conflict(),
            _ => Ok(new ApiResponse<string>(outcome == LecturerReviewOutcome.Approved ? "APPROVED" : outcome.ToString()))
        };
    }

    [HttpPost("lecturers/{lecturerId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RejectLecturer(
        Guid lecturerId, [FromBody] AdminVerificationDecisionRequest? request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.LecturersApprove)) return Forbid();

        var outcome = await verificationService.DecideLecturerAsync(
            currentUser.UserId!.Value, lecturerId, approve: false, request?.Note, cancellationToken);
        return outcome switch
        {
            LecturerReviewOutcome.NotFound => NotFound(),
            LecturerReviewOutcome.AlreadyProcessed => Conflict(),
            _ => Ok(new ApiResponse<string>(outcome == LecturerReviewOutcome.Rejected ? "REJECTED" : outcome.ToString()))
        };
    }

    [HttpGet("companies")]
    [ProducesResponseType(typeof(IReadOnlyCollection<AdminCompanyReviewItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<AdminCompanyReviewItem>>> ListCompanies(
        [FromQuery] string? status, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.CompaniesVerify)) return Forbid();

        return Ok(new ApiResponse<IReadOnlyCollection<AdminCompanyReviewItem>>(await verificationService.ListCompaniesAsync(status, cancellationToken)));
    }

    [HttpPost("companies/{companyId:guid}/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> VerifyCompany(
        Guid companyId, [FromBody] AdminVerificationDecisionRequest? request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.CompaniesVerify)) return Forbid();

        var outcome = await verificationService.DecideCompanyAsync(
            currentUser.UserId!.Value, companyId, approve: true, request?.Note, cancellationToken);
        return outcome switch
        {
            CompanyReviewOutcome.NotFound => NotFound(),
            CompanyReviewOutcome.AlreadyProcessed => Conflict(),
            _ => Ok(new ApiResponse<string>(outcome == CompanyReviewOutcome.Verified ? "VERIFIED" : outcome.ToString()))
        };
    }

    [HttpPost("companies/{companyId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RejectCompany(
        Guid companyId, [FromBody] AdminVerificationDecisionRequest? request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.CompaniesVerify)) return Forbid();

        var outcome = await verificationService.DecideCompanyAsync(
            currentUser.UserId!.Value, companyId, approve: false, request?.Note, cancellationToken);
        return outcome switch
        {
            CompanyReviewOutcome.NotFound => NotFound(),
            CompanyReviewOutcome.AlreadyProcessed => Conflict(),
            _ => Ok(new ApiResponse<string>(outcome == CompanyReviewOutcome.Rejected ? "REJECTED" : outcome.ToString()))
        };
    }
}
