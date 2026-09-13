using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1")]
[Authorize]
[Produces("application/json")]
public sealed class ProjectTasksController(IProjectTaskService taskService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("projects/{projectId:guid}/tasks")]
    public async Task<ActionResult<ApiResponse<ProjectTaskResponse>>> Create(Guid projectId, CreateProjectTaskRequest request, CancellationToken cancellationToken) =>
        ToResult(await taskService.CreateAsync(UserId, projectId, request, cancellationToken), created: true);

    [HttpGet("projects/{projectId:guid}/tasks")]
    public async Task<ActionResult<KanbanBoardResponse>> List(Guid projectId, CancellationToken cancellationToken)
    {
        var board = await taskService.BoardAsync(UserId, projectId, cancellationToken);
        return board is null ? NotFound() : Ok(board);
    }

    [HttpGet("projects/{projectId:guid}/board")]
    public async Task<ActionResult<KanbanBoardResponse>> Board(Guid projectId, CancellationToken cancellationToken)
    {
        var board = await taskService.BoardAsync(UserId, projectId, cancellationToken);
        return board is null ? NotFound() : Ok(board);
    }

    [HttpGet("tasks/{taskId:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectTaskResponse>>> Get(Guid taskId, CancellationToken cancellationToken) =>
        ToResult(await taskService.GetAsync(UserId, taskId, cancellationToken));

    [HttpPut("tasks/{taskId:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectTaskResponse>>> Update(Guid taskId, UpdateProjectTaskRequest request, CancellationToken cancellationToken) =>
        ToResult(await taskService.UpdateAsync(UserId, taskId, request, cancellationToken));

    [HttpDelete("tasks/{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid taskId, [FromQuery] Guid version, CancellationToken cancellationToken)
    {
        var outcome = await taskService.DeleteAsync(UserId, taskId, version, cancellationToken);
        return outcome switch
        {
            ProjectTaskOutcome.Success => NoContent(),
            ProjectTaskOutcome.NotFound => NotFound(),
            ProjectTaskOutcome.Forbidden => Forbid(),
            ProjectTaskOutcome.Conflict => Conflict(),
            _ => BadRequest()
        };
    }

    [HttpPost("tasks/{taskId:guid}/move")]
    public async Task<ActionResult<ApiResponse<ProjectTaskResponse>>> Move(Guid taskId, MoveProjectTaskRequest request, CancellationToken cancellationToken) =>
        ToResult(await taskService.MoveAsync(UserId, taskId, request, cancellationToken));

    [HttpPost("tasks/{taskId:guid}/assign")]
    public async Task<ActionResult<ApiResponse<ProjectTaskResponse>>> Assign(Guid taskId, AssignProjectTaskRequest request, CancellationToken cancellationToken) =>
        ToResult(await taskService.AssignAsync(UserId, taskId, request, cancellationToken));

    [HttpPost("tasks/{taskId:guid}/unassign")]
    public Task<ActionResult<ApiResponse<ProjectTaskResponse>>> Unassign(Guid taskId, Guid version, CancellationToken cancellationToken) =>
        Assign(taskId, new AssignProjectTaskRequest { Version = version }, cancellationToken);

    private ActionResult<ApiResponse<ProjectTaskResponse>> ToResult((ProjectTaskOutcome Outcome, ProjectTaskResponse? Task) result, bool created = false) => result.Outcome switch
    {
        ProjectTaskOutcome.Success when result.Task is not null => created ? Created($"tasks/{result.Task.Id}", new ApiResponse<ProjectTaskResponse>(result.Task)) : Ok(new ApiResponse<ProjectTaskResponse>(result.Task)),
        ProjectTaskOutcome.NotFound => NotFound(),
        ProjectTaskOutcome.Forbidden => Forbid(),
        ProjectTaskOutcome.Conflict => Conflict(),
        _ => BadRequest()
    };

    private Guid UserId => currentUser.UserId!.Value;
}
