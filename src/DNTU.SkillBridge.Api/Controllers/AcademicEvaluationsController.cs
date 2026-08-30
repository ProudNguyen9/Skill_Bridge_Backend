using Asp.Versioning;
using DNTU.SkillBridge.Api.Academics;
using DNTU.SkillBridge.Application.Common;
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
public sealed class AcademicEvaluationsController(AcademicEvaluationService evaluationService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("lecturer/evaluations")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<AcademicEvaluationResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AcademicEvaluationResponse>>>> ListMine(CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Lecturer)) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<AcademicEvaluationResponse>>(await evaluationService.ListForLecturerAsync(currentUser.UserId!.Value, cancellationToken)));
    }

    [HttpGet("projects/{projectId:guid}/students/{studentId:guid}/evaluation")]
    [ProducesResponseType(typeof(ApiResponse<AcademicEvaluationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AcademicEvaluationResponse>>> Get(
        Guid projectId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var evaluation = await evaluationService.GetAsync(currentUser.UserId!.Value, projectId, studentId, cancellationToken);
        return evaluation is null ? NotFound() : Ok(new ApiResponse<AcademicEvaluationResponse>(evaluation));
    }

    [HttpPost("projects/{projectId:guid}/students/{studentId:guid}/evaluation")]
    public async Task<ActionResult<ApiResponse<AcademicEvaluationResponse>>> Upsert(
        Guid projectId,
        Guid studentId,
        UpsertAcademicEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Lecturer)) return Forbid();
        var evaluation = await evaluationService.UpsertAsync(currentUser.UserId!.Value, projectId, studentId, request, cancellationToken);
        return evaluation is null ? BadRequest() : Ok(new ApiResponse<AcademicEvaluationResponse>(evaluation));
    }

    [HttpPost("projects/{projectId:guid}/students/{studentId:guid}/evaluation/finalize")]
    public async Task<ActionResult<ApiResponse<AcademicEvaluationResponse>>> Finalize(
        Guid projectId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Lecturer)) return Forbid();
        var evaluation = await evaluationService.FinalizeAsync(currentUser.UserId!.Value, projectId, studentId, cancellationToken);
        return evaluation is null ? Conflict() : Ok(new ApiResponse<AcademicEvaluationResponse>(evaluation));
    }
}
