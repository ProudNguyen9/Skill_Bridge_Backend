using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
[Produces("application/json")]
public sealed class TaskCollaborationController(ITaskCollaborationService collaborationService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("tasks/{taskId:guid}/comments")]
    public async Task<ActionResult<ApiResponse<TaskCommentResponse>>> AddComment(Guid taskId, CreateTaskCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await collaborationService.AddCommentAsync(currentUser, taskId, request, cancellationToken);
        return comment is null ? NotFound() : Created(string.Empty, new ApiResponse<TaskCommentResponse>(comment));
    }

    [HttpGet("tasks/{taskId:guid}/comments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TaskCommentResponse>>>> ListComments(Guid taskId, CancellationToken cancellationToken)
    {
        var comments = await collaborationService.ListCommentsAsync(currentUser, taskId, cancellationToken);
        return comments is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<TaskCommentResponse>>(comments));
    }

    [HttpDelete("tasks/{taskId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid taskId, Guid commentId, CancellationToken cancellationToken) =>
        await collaborationService.DeleteCommentAsync(currentUser, taskId, commentId, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("tasks/{taskId:guid}/checklist-items")]
    public async Task<ActionResult<ApiResponse<TaskChecklistItemResponse>>> AddChecklist(Guid taskId, CreateChecklistItemRequest request, CancellationToken cancellationToken) =>
        ToChecklistResult(await collaborationService.AddChecklistItemAsync(currentUser, taskId, request, cancellationToken), created: true);

    [HttpPatch("checklist-items/{itemId:guid}")]
    public async Task<ActionResult<ApiResponse<TaskChecklistItemResponse>>> UpdateChecklist(Guid itemId, UpdateChecklistItemRequest request, CancellationToken cancellationToken) =>
        ToChecklistResult(await collaborationService.UpdateChecklistItemAsync(currentUser, itemId, request, cancellationToken));

    [HttpDelete("checklist-items/{itemId:guid}")]
    public async Task<IActionResult> DeleteChecklist(Guid itemId, [FromQuery] Guid version, CancellationToken cancellationToken)
    {
        var outcome = await collaborationService.DeleteChecklistItemAsync(currentUser, itemId, version, cancellationToken);
        return outcome switch
        {
            TaskCollaborationOutcome.Success => NoContent(),
            TaskCollaborationOutcome.NotFound => NotFound(),
            TaskCollaborationOutcome.Forbidden => Forbid(),
            TaskCollaborationOutcome.Conflict => Conflict(),
            _ => BadRequest()
        };
    }

    [HttpGet("students/me/tasks")]
    [ProducesResponseType(typeof(PagedResponse<ProjectTaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<ProjectTaskResponse>>> MyTasks([FromQuery] TaskListQuery query, CancellationToken cancellationToken)
    {
        var tasks = await collaborationService.GetMyStudentTasksAsync(currentUser, query, cancellationToken);
        return tasks is null ? Forbid() : Ok(tasks);
    }

    [HttpGet("lecturer/tasks")]
    [ProducesResponseType(typeof(PagedResponse<ProjectTaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<ProjectTaskResponse>>> LecturerTasks([FromQuery] TaskListQuery query, CancellationToken cancellationToken)
    {
        var tasks = await collaborationService.GetLecturerTasksAsync(currentUser, query, cancellationToken);
        return tasks is null ? Forbid() : Ok(tasks);
    }

    private ActionResult<ApiResponse<TaskChecklistItemResponse>> ToChecklistResult((TaskCollaborationOutcome Outcome, TaskChecklistItemResponse? Item) result, bool created = false) => result.Outcome switch
    {
        TaskCollaborationOutcome.Success when result.Item is not null => created
            ? Created($"checklist-items/{result.Item.Id}", new ApiResponse<TaskChecklistItemResponse>(result.Item))
            : Ok(new ApiResponse<TaskChecklistItemResponse>(result.Item)),
        TaskCollaborationOutcome.NotFound => NotFound(),
        TaskCollaborationOutcome.Forbidden => Forbid(),
        TaskCollaborationOutcome.Conflict => Conflict(),
        _ => BadRequest()
    };
}
