using Asp.Versioning;
using DNTU.SkillBridge.Api.Contracts;
using DNTU.SkillBridge.Api.Submissions;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

/// <summary>Company-scoped business review endpoints; DTOs intentionally exclude academic evaluation fields.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
[Produces("application/json")]
public sealed class BusinessReviewsController(BusinessReviewService reviewService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Creates an immutable business decision for the current technically-approved submission version.</summary>
    [HttpPost("company/submissions/{submissionId:guid}/business-review")]
    [ProducesResponseType(typeof(ApiResponse<BusinessReviewResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<BusinessReviewResponse>>> Create(
        Guid submissionId,
        [FromBody] CreateBusinessReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanReviewBusiness()) return Forbid();

        var (outcome, review) = await reviewService.CreateAsync(UserId, submissionId, request, cancellationToken);
        return outcome switch
        {
            BusinessReviewOutcome.Success when review is not null => Created($"company/business-reviews/{review.Id}", new ApiResponse<BusinessReviewResponse>(review)),
            BusinessReviewOutcome.Forbidden => Forbid(),
            BusinessReviewOutcome.NotFound => NotFound(),
            BusinessReviewOutcome.Invalid => BadRequest(),
            _ => Conflict()
        };
    }

    /// <summary>Idempotent-compatible alias of POST for clients using the documented POST/PUT review contract.</summary>
    [HttpPut("company/submissions/{submissionId:guid}/business-review")]
    [ProducesResponseType(typeof(ApiResponse<BusinessReviewResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<BusinessReviewResponse>>> Put(
        Guid submissionId,
        [FromBody] CreateBusinessReviewRequest request,
        CancellationToken cancellationToken) => Create(submissionId, request, cancellationToken);

    /// <summary>Lists business-review history for a submission only within its project scope.</summary>
    [HttpGet("submissions/{submissionId:guid}/business-reviews")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<BusinessReviewResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<BusinessReviewResponse>>>> List(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var (outcome, reviews) = await reviewService.ListForSubmissionAsync(UserId, submissionId, cancellationToken);
        return outcome switch
        {
            BusinessReviewOutcome.Success when reviews is not null => Ok(new ApiResponse<IReadOnlyCollection<BusinessReviewResponse>>(reviews)),
            BusinessReviewOutcome.Forbidden => Forbid(),
            _ => NotFound()
        };
    }

    /// <summary>Lists the signed-in company reviewer's immutable business-review history.</summary>
    [HttpGet("company/business-reviews")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<BusinessReviewResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<BusinessReviewResponse>>>> ListMine(CancellationToken cancellationToken)
    {
        if (!IsCompany()) return Forbid();
        var reviews = await reviewService.ListMineAsync(UserId, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyCollection<BusinessReviewResponse>>(reviews));
    }

    private bool CanReviewBusiness() => IsCompany() &&
        (currentUser.Permissions.Contains(PermissionNames.SubmissionsReviewBusiness) || currentUser.Permissions.Count == 0);
    private bool IsCompany() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Company);
    private Guid UserId => currentUser.UserId!.Value;
}
