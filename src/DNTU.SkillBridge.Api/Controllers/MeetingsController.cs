using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Application.Meetings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
[Produces("application/json")]
public sealed class MeetingsController(IMeetingService meetingService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Schedules a UTC project meeting. Project members may schedule only when workspace settings allow it; owners, active lecturers, and admins may always schedule.</summary>
    [HttpPost("projects/{projectId:guid}/meetings")]
    [ProducesResponseType(typeof(ApiResponse<MeetingResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<MeetingResponse>>> Create(Guid projectId, [FromBody] CreateMeetingRequest request, CancellationToken cancellationToken)
    {
        var meeting = await meetingService.CreateAsync(UserId, projectId, request, cancellationToken);
        return meeting is null ? BadRequest() : Created($"meetings/{meeting.Id}", new ApiResponse<MeetingResponse>(meeting));
    }

    /// <summary>Lists meetings visible to a scoped project participant, company owner, assigned lecturer, or administrator.</summary>
    [HttpGet("projects/{projectId:guid}/meetings")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<MeetingResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MeetingResponse>>>> List(Guid projectId, CancellationToken cancellationToken)
    {
        var meetings = await meetingService.ListAsync(UserId, projectId, cancellationToken);
        return meetings is null ? Forbid() : Ok(new ApiResponse<IReadOnlyCollection<MeetingResponse>>(meetings));
    }

    [HttpGet("meetings/{meetingId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MeetingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<MeetingResponse>>> Get(Guid meetingId, CancellationToken cancellationToken)
    {
        var meeting = await meetingService.GetAsync(UserId, meetingId, cancellationToken);
        return meeting is null ? NotFound() : Ok(new ApiResponse<MeetingResponse>(meeting));
    }

    [HttpPut("meetings/{meetingId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MeetingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<MeetingResponse>>> Update(Guid meetingId, [FromBody] UpdateMeetingRequest request, CancellationToken cancellationToken)
    {
        var meeting = await meetingService.UpdateAsync(UserId, meetingId, request, cancellationToken);
        return meeting is null ? BadRequest() : Ok(new ApiResponse<MeetingResponse>(meeting));
    }

    [HttpDelete("meetings/{meetingId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid meetingId, CancellationToken cancellationToken) =>
        await meetingService.DeleteAsync(UserId, meetingId, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("meetings/{meetingId:guid}/participants")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<MeetingParticipantResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MeetingParticipantResponse>>>> Participants(Guid meetingId, CancellationToken cancellationToken)
    {
        var participants = await meetingService.GetParticipantsAsync(UserId, meetingId, cancellationToken);
        return participants is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<MeetingParticipantResponse>>(participants));
    }

    /// <summary>Records attendance. Only company owners, active assigned lecturers, and administrators may update attendance.</summary>
    [HttpPut("meetings/{meetingId:guid}/attendance")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Attendance(Guid meetingId, [FromBody] UpdateAttendanceRequest request, CancellationToken cancellationToken) =>
        await meetingService.UpdateAttendanceAsync(UserId, meetingId, request, cancellationToken) ? NoContent() : Forbid();

    /// <summary>Reads minutes only for a scoped workspace participant, company owner, assigned lecturer, or administrator.</summary>
    [HttpGet("meetings/{meetingId:guid}/minutes")]
    [ProducesResponseType(typeof(ApiResponse<MeetingMinutesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<MeetingMinutesResponse>>> GetMinutes(Guid meetingId, CancellationToken cancellationToken) =>
        ToResult(await meetingService.GetMinutesAsync(UserId, meetingId, cancellationToken));

    /// <summary>Creates or replaces minutes. Only company owners, active assigned lecturers, and administrators can edit; replacements preserve an immutable revision.</summary>
    [HttpPut("meetings/{meetingId:guid}/minutes")]
    [ProducesResponseType(typeof(ApiResponse<MeetingMinutesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<MeetingMinutesResponse>>> PutMinutes(Guid meetingId, UpsertMeetingMinutesRequest request, CancellationToken cancellationToken) =>
        ToResult(await meetingService.UpsertMinutesAsync(UserId, meetingId, request, cancellationToken));

    [HttpPost("meetings/{meetingId:guid}/action-items")]
    [ProducesResponseType(typeof(ApiResponse<MeetingActionItemResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<MeetingActionItemResponse>>> CreateActionItem(Guid meetingId, CreateMeetingActionItemRequest request, CancellationToken cancellationToken) =>
        ToResult(await meetingService.CreateActionItemAsync(UserId, meetingId, request, cancellationToken), true);

    [HttpPut("meeting-action-items/{actionItemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MeetingActionItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<MeetingActionItemResponse>>> UpdateActionItem(Guid actionItemId, UpdateMeetingActionItemRequest request, CancellationToken cancellationToken) =>
        ToResult(await meetingService.UpdateActionItemAsync(UserId, actionItemId, request, cancellationToken));

    [HttpDelete("meeting-action-items/{actionItemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteActionItem(Guid actionItemId, [FromQuery] Guid version, CancellationToken cancellationToken) =>
        ToResult(await meetingService.DeleteActionItemAsync(UserId, actionItemId, version, cancellationToken));

    /// <summary>Atomically converts one action item to its sole linked Kanban task. Repeated requests return that existing task.</summary>
    [HttpPost("meeting-action-items/{actionItemId:guid}/convert-to-task")]
    [ProducesResponseType(typeof(ApiResponse<MeetingActionItemConversionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<MeetingActionItemConversionResponse>>> ConvertActionItem(Guid actionItemId, ConvertMeetingActionItemRequest request, CancellationToken cancellationToken) =>
        ToConversionResult(await meetingService.ConvertActionItemToTaskAsync(UserId, actionItemId, request, cancellationToken));

    [HttpGet("students/me/meetings")]
    [Authorize(Roles = "STUDENT")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<MeetingResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MeetingResponse>>>> StudentMeetings(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyCollection<MeetingResponse>>(await meetingService.ListStudentMeetingsAsync(UserId, cancellationToken)));

    [HttpGet("company/meetings")]
    [Authorize(Roles = "COMPANY")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<MeetingResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MeetingResponse>>>> CompanyMeetings(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyCollection<MeetingResponse>>(await meetingService.ListCompanyMeetingsAsync(UserId, cancellationToken)));

    [HttpGet("lecturer/meetings")]
    [Authorize(Roles = "LECTURER")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<MeetingResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MeetingResponse>>>> LecturerMeetings(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyCollection<MeetingResponse>>(await meetingService.ListLecturerMeetingsAsync(UserId, cancellationToken)));

    private ActionResult<ApiResponse<T>> ToResult<T>((MeetingOutcome Outcome, T? Value) result, bool created = false) where T : class => result.Outcome switch
    {
        MeetingOutcome.Success when result.Value is not null => created ? Created(string.Empty, new ApiResponse<T>(result.Value)) : Ok(new ApiResponse<T>(result.Value)),
        MeetingOutcome.NotFound => NotFound(),
        MeetingOutcome.Forbidden => Forbid(),
        MeetingOutcome.Conflict => Conflict(),
        _ => BadRequest()
    };

    private IActionResult ToResult(MeetingOutcome outcome) => outcome switch
    {
        MeetingOutcome.Success => NoContent(),
        MeetingOutcome.NotFound => NotFound(),
        MeetingOutcome.Forbidden => Forbid(),
        MeetingOutcome.Conflict => Conflict(),
        _ => BadRequest()
    };

    private ActionResult<ApiResponse<MeetingActionItemConversionResponse>> ToConversionResult((MeetingOutcome Outcome, MeetingActionItemConversionResponse? Conversion) result) =>
        ToResult(result);

    private Guid UserId => currentUser.UserId!.Value;
}
