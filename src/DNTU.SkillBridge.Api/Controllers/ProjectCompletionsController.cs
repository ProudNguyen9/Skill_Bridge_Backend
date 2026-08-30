using Asp.Versioning;
using DNTU.SkillBridge.Api.Contracts;
using DNTU.SkillBridge.Api.Projects;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects")]
[Authorize]
[Produces("application/json")]
public sealed class ProjectCompletionsController(ProjectCompletionService completionService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("{projectId:guid}/complete")]
    [ProducesResponseType(typeof(ApiResponse<ProjectCompletionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ProjectCompletionResponse>>> Complete(Guid projectId, CancellationToken cancellationToken)
    {
        var isAdministrator = currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin);
        var (outcome, completion) = await completionService.CompleteAsync(currentUser.UserId!.Value, isAdministrator, projectId, cancellationToken);
        return outcome switch
        {
            ProjectCompletionOutcome.Success when completion is not null => Ok(new ApiResponse<ProjectCompletionResponse>(completion)),
            ProjectCompletionOutcome.NotFound => NotFound(),
            ProjectCompletionOutcome.Forbidden => Forbid(),
            _ => Conflict()
        };
    }

    [HttpGet("{projectId:guid}/completion")]
    [ProducesResponseType(typeof(ApiResponse<ProjectCompletionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProjectCompletionResponse>>> Get(Guid projectId, CancellationToken cancellationToken)
    {
        var completion = await completionService.GetAsync(projectId, cancellationToken);
        return completion is null ? NotFound() : Ok(new ApiResponse<ProjectCompletionResponse>(completion));
    }
}
