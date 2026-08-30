using Asp.Versioning;
using DNTU.SkillBridge.Api.Contracts;
using DNTU.SkillBridge.Api.Milestones;
using DNTU.SkillBridge.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
[Produces("application/json")]
public sealed class MilestonesController(MilestoneService milestoneService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Creates a planned milestone. Company owners/managers and active assigned lecturers manage the milestone plan.</summary>
    [HttpPost("projects/{projectId:guid}/milestones")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<MilestoneResponse>>> Create(Guid projectId, [FromBody] CreateMilestoneRequest request, CancellationToken cancellationToken)
    {
        var milestone = await milestoneService.CreateAsync(UserId, projectId, request, cancellationToken);
        return milestone is null ? Conflict() : Created($"milestones/{milestone.Id}", new ApiResponse<MilestoneResponse>(milestone));
    }

    /// <summary>Lists milestone plans and their deliverables/history for a project participant.</summary>
    [HttpGet("projects/{projectId:guid}/milestones")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<MilestoneResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MilestoneResponse>>>> List(Guid projectId, CancellationToken cancellationToken)
    {
        var milestones = await milestoneService.ListAsync(UserId, projectId, cancellationToken);
        return milestones is null ? Forbid() : Ok(new ApiResponse<IReadOnlyCollection<MilestoneResponse>>(milestones));
    }

    [HttpGet("milestones/{milestoneId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<MilestoneResponse>>> Get(Guid milestoneId, CancellationToken cancellationToken)
    {
        var milestone = await milestoneService.GetAsync(UserId, milestoneId, cancellationToken);
        return milestone is null ? NotFound() : Ok(new ApiResponse<MilestoneResponse>(milestone));
    }

    /// <summary>Updates a non-approved milestone with an optimistic-concurrency version.</summary>
    [HttpPut("milestones/{milestoneId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<MilestoneResponse>>> Update(Guid milestoneId, [FromBody] UpdateMilestoneRequest request, CancellationToken cancellationToken)
    {
        var milestone = await milestoneService.UpdateAsync(UserId, milestoneId, request, cancellationToken);
        return milestone is null ? Conflict() : Ok(new ApiResponse<MilestoneResponse>(milestone));
    }

    [HttpDelete("milestones/{milestoneId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid milestoneId, [FromQuery] Guid version, CancellationToken cancellationToken) =>
        await milestoneService.DeleteAsync(UserId, milestoneId, version, cancellationToken) ? NoContent() : Conflict();

    /// <summary>Moves the milestone through start, submit, request-revision, or approve. Approval/revision require company management or active lecturer supervision.</summary>
    [HttpPost("milestones/{milestoneId:guid}/start")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<MilestoneResponse>>> Start(Guid milestoneId, [FromBody] TransitionMilestoneRequest request, CancellationToken cancellationToken) =>
        Transition(milestoneId, "start", request, cancellationToken);

    [HttpPost("milestones/{milestoneId:guid}/submit")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<MilestoneResponse>>> Submit(Guid milestoneId, [FromBody] TransitionMilestoneRequest request, CancellationToken cancellationToken) =>
        Transition(milestoneId, "submit", request, cancellationToken);

    [HttpPost("milestones/{milestoneId:guid}/request-revision")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<MilestoneResponse>>> RequestRevision(Guid milestoneId, [FromBody] TransitionMilestoneRequest request, CancellationToken cancellationToken) =>
        Transition(milestoneId, "request-revision", request, cancellationToken);

    [HttpPost("milestones/{milestoneId:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<MilestoneResponse>>> Approve(Guid milestoneId, [FromBody] TransitionMilestoneRequest request, CancellationToken cancellationToken) =>
        Transition(milestoneId, "approve", request, cancellationToken);

    private async Task<ActionResult<ApiResponse<MilestoneResponse>>> Transition(Guid milestoneId, string action, TransitionMilestoneRequest request, CancellationToken cancellationToken)
    {
        var milestone = await milestoneService.TransitionAsync(UserId, milestoneId, action, request, cancellationToken);
        return milestone is null ? Conflict() : Ok(new ApiResponse<MilestoneResponse>(milestone));
    }

    [HttpGet("milestones/{milestoneId:guid}/deliverables")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<MilestoneDeliverableResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MilestoneDeliverableResponse>>>> ListDeliverables(Guid milestoneId, CancellationToken cancellationToken)
    {
        var deliverables = await milestoneService.ListDeliverablesAsync(UserId, milestoneId, cancellationToken);
        return deliverables is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<MilestoneDeliverableResponse>>(deliverables));
    }

    [HttpPost("milestones/{milestoneId:guid}/deliverables")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneDeliverableResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<MilestoneDeliverableResponse>>> AddDeliverable(Guid milestoneId, [FromBody] CreateMilestoneDeliverableRequest request, CancellationToken cancellationToken)
    {
        var deliverable = await milestoneService.AddDeliverableAsync(UserId, milestoneId, request, cancellationToken);
        return deliverable is null ? Conflict() : Created($"milestone-deliverables/{deliverable.Id}", new ApiResponse<MilestoneDeliverableResponse>(deliverable));
    }

    [HttpPut("milestone-deliverables/{deliverableId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MilestoneDeliverableResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<MilestoneDeliverableResponse>>> UpdateDeliverable(Guid deliverableId, [FromBody] UpdateMilestoneDeliverableRequest request, CancellationToken cancellationToken)
    {
        var deliverable = await milestoneService.UpdateDeliverableAsync(UserId, deliverableId, request, cancellationToken);
        return deliverable is null ? Conflict() : Ok(new ApiResponse<MilestoneDeliverableResponse>(deliverable));
    }

    [HttpDelete("milestone-deliverables/{deliverableId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteDeliverable(Guid deliverableId, CancellationToken cancellationToken) =>
        await milestoneService.DeleteDeliverableAsync(UserId, deliverableId, cancellationToken) ? NoContent() : Conflict();

    private Guid UserId => currentUser.UserId!.Value;
}
