using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Submissions;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>Immutable lecturer technical-review history for submission evidence versions.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1")]
[Authorize]
[Produces("application/json")]
public sealed class TechnicalReviewsController(ITechnicalReviewService reviewService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Records one assigned lecturer's technical decision and atomically moves the submission workflow.</summary>
    [HttpPost("lecturer/submissions/{submissionId:guid}/technical-review")]
    [ProducesResponseType(typeof(ApiResponse<TechnicalReviewResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<TechnicalReviewResponse>>> Create(
        Guid submissionId,
        [FromBody] CreateTechnicalReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanReviewTechnical()) return Forbid();

        var (outcome, review) = await reviewService.CreateAsync(
            UserId,
            IsAdminOverride(),
            submissionId,
            request,
            cancellationToken);
        return outcome switch
        {
            TechnicalReviewOutcome.Success when review is not null => Created($"lecturer/reviews/{review.Id}", new ApiResponse<TechnicalReviewResponse>(review)),
            TechnicalReviewOutcome.Forbidden => Forbid(),
            TechnicalReviewOutcome.NotFound => NotFound(),
            TechnicalReviewOutcome.Invalid => BadRequest(),
            _ => Conflict()
        };
    }

    /// <summary>Lists review history recorded by the signed-in lecturer.</summary>
    [HttpGet("lecturer/reviews")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<TechnicalReviewResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TechnicalReviewResponse>>>> ListMine(CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        var reviews = await reviewService.ListMineAsync(UserId, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyCollection<TechnicalReviewResponse>>(reviews));
    }

    /// <summary>Gets an immutable technical review when the caller can access its supervised project.</summary>
    [HttpGet("lecturer/reviews/{reviewId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TechnicalReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TechnicalReviewResponse>>> Get(Guid reviewId, CancellationToken cancellationToken)
    {
        if (!CanReviewTechnical()) return Forbid();

        var (outcome, review) = await reviewService.GetAsync(UserId, IsAdminOverride(), reviewId, cancellationToken);
        return outcome switch
        {
            TechnicalReviewOutcome.Success when review is not null => Ok(new ApiResponse<TechnicalReviewResponse>(review)),
            TechnicalReviewOutcome.Forbidden => Forbid(),
            _ => NotFound()
        };
    }

    /// <summary>Lists technical-review history for a submission. Company users have no access to academic review records.</summary>
    [HttpGet("submissions/{submissionId:guid}/reviews")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<TechnicalReviewResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TechnicalReviewResponse>>>> List(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var (outcome, reviews) = await reviewService.ListForSubmissionAsync(UserId, IsAdminOverride(), submissionId, cancellationToken);
        return outcome switch
        {
            TechnicalReviewOutcome.Success when reviews is not null => Ok(new ApiResponse<IReadOnlyCollection<TechnicalReviewResponse>>(reviews)),
            TechnicalReviewOutcome.Forbidden => Forbid(),
            _ => NotFound()
        };
    }

    private bool CanReviewTechnical() => IsLecturer() || IsAdminOverride();
    private bool IsLecturer() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Lecturer);
    private bool IsAdminOverride() => currentUser.IsAuthenticated &&
        (currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin)) &&
        (currentUser.Permissions.Contains(PermissionNames.SubmissionsReviewTechnical) || currentUser.Permissions.Count == 0);
    private Guid UserId => currentUser.UserId!.Value;
}
