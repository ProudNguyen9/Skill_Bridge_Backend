using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Teams;
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
public sealed class TeamsController(ITeamService teamService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("teams")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<TeamResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TeamResponse>>>> ListMine(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<TeamResponse>>(await teamService.ListMineAsync(UserId, cancellationToken)));
    }

    [HttpPost("teams")]
    [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<TeamResponse>>> Create(CreateTeamRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var team = await teamService.CreateAsync(UserId, request, cancellationToken);
        return Created($"teams/{team.Id}", new ApiResponse<TeamResponse>(team));
    }

    [HttpGet("teams/{teamId:guid}")]
    public async Task<ActionResult<ApiResponse<TeamResponse>>> Get(Guid teamId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var team = await teamService.GetAsync(UserId, teamId, cancellationToken);
        return team is null ? NotFound() : Ok(new ApiResponse<TeamResponse>(team));
    }

    [HttpPut("teams/{teamId:guid}")]
    public async Task<ActionResult<ApiResponse<TeamResponse>>> Update(Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var (outcome, team) = await teamService.UpdateAsync(UserId, teamId, request, cancellationToken);
        return Outcome(outcome, team);
    }

    [HttpPut("teams/{teamId:guid}/avatar")]
    public async Task<ActionResult<ApiResponse<TeamResponse>>> UpdateAvatar(Guid teamId, UpdateTeamAvatarRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var (outcome, team) = await teamService.UpdateAvatarAsync(UserId, teamId, request.AvatarUrl, cancellationToken);
        return Outcome(outcome, team);
    }

    [HttpDelete("teams/{teamId:guid}")]
    public async Task<IActionResult> Delete(Guid teamId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return (await teamService.DeleteAsync(UserId, teamId, cancellationToken)) switch { TeamOperationOutcome.Success => NoContent(), TeamOperationOutcome.NotFound => NotFound(), TeamOperationOutcome.Forbidden => Forbid(), TeamOperationOutcome.Locked => Conflict(), _ => Conflict() };
    }

    [HttpGet("teams/{teamId:guid}/members")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TeamMemberResponse>>>> Members(Guid teamId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var team = await teamService.GetAsync(UserId, teamId, cancellationToken);
        return team is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<TeamMemberResponse>>(team.Members));
    }

    [HttpPost("teams/{teamId:guid}/invitations")]
    public async Task<ActionResult<ApiResponse<TeamInvitationResponse>>> Invite(Guid teamId, InviteTeamMemberRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var (outcome, invitation) = await teamService.InviteAsync(UserId, teamId, request, cancellationToken);
        return outcome == TeamOperationOutcome.Success && invitation is not null ? Ok(new ApiResponse<TeamInvitationResponse>(invitation)) : Outcome<TeamInvitationResponse>(outcome, null);
    }

    [HttpGet("teams/{teamId:guid}/invitations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TeamInvitationResponse>>>> TeamInvitations(Guid teamId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var items = await teamService.ListTeamInvitationsAsync(UserId, teamId, cancellationToken);
        return items is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<TeamInvitationResponse>>(items));
    }

    [HttpDelete("teams/{teamId:guid}/invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid teamId, Guid invitationId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return SimpleOutcome(await teamService.RevokeInvitationAsync(UserId, teamId, invitationId, cancellationToken));
    }

    [HttpGet("students/me/team-invitations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TeamInvitationResponse>>>> Invitations(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<TeamInvitationResponse>>(await teamService.ListMyInvitationsAsync(UserId, cancellationToken)));
    }

    [HttpPost("team-invitations/{invitationId:guid}/accept")]
    public Task<IActionResult> Accept(Guid invitationId, CancellationToken cancellationToken) => Respond(invitationId, true, cancellationToken);

    [HttpPost("team-invitations/{invitationId:guid}/reject")]
    public Task<IActionResult> Reject(Guid invitationId, CancellationToken cancellationToken) => Respond(invitationId, false, cancellationToken);

    [HttpPut("teams/{teamId:guid}/members/{studentId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid teamId, Guid studentId, UpdateMemberFunctionalRoleRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return SimpleOutcome(await teamService.UpdateMemberFunctionalRoleAsync(UserId, teamId, studentId, request.FunctionalRole, cancellationToken));
    }

    [HttpDelete("teams/{teamId:guid}/members/{studentId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid teamId, Guid studentId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return SimpleOutcome(await teamService.RemoveMemberAsync(UserId, teamId, studentId, cancellationToken));
    }

    [HttpPut("teams/{teamId:guid}/leader")]
    public async Task<IActionResult> ChangeLeader(Guid teamId, ChangeTeamLeaderRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return SimpleOutcome(await teamService.ChangeLeaderAsync(UserId, teamId, request.StudentId, cancellationToken));
    }

    [HttpGet("teams/{teamId:guid}/recommendations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TeamProjectRecommendationResponse>>>> TeamRecommendations(Guid teamId, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var items = await teamService.ListTeamRecommendationsAsync(UserId, teamId, cancellationToken);
        return items is null ? NotFound() : Ok(new ApiResponse<IReadOnlyCollection<TeamProjectRecommendationResponse>>(items));
    }

    [HttpGet("students/me/team-recommendations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TeamProjectRecommendationResponse>>>> MyRecommendations(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<TeamProjectRecommendationResponse>>(await teamService.ListMyTeamRecommendationsAsync(UserId, cancellationToken)));
    }

    [HttpPost("team-recommendations/{recommendationId:guid}/respond")]
    public async Task<IActionResult> RespondRecommendation(Guid recommendationId, RespondRecommendationRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return SimpleOutcome(await teamService.RespondRecommendationAsync(UserId, recommendationId, request.Accept, request.Note, cancellationToken));
    }

    private async Task<IActionResult> Respond(Guid invitationId, bool accept, CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        return SimpleOutcome(await teamService.RespondAsync(UserId, invitationId, accept, cancellationToken));
    }

    private ActionResult<ApiResponse<T>> Outcome<T>(TeamOperationOutcome outcome, T? response) where T : class => outcome switch
    {
        TeamOperationOutcome.Success when response is not null => Ok(new ApiResponse<T>(response)),
        TeamOperationOutcome.NotFound => NotFound(),
        TeamOperationOutcome.Forbidden => Forbid(),
        TeamOperationOutcome.Locked or TeamOperationOutcome.Conflict => Conflict(),
        _ => BadRequest()
    };
    private IActionResult SimpleOutcome(TeamOperationOutcome outcome) => outcome switch { TeamOperationOutcome.Success => NoContent(), TeamOperationOutcome.NotFound => NotFound(), TeamOperationOutcome.Forbidden => Forbid(), TeamOperationOutcome.Locked or TeamOperationOutcome.Conflict => Conflict(), _ => BadRequest() };
    private bool IsStudent() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Student);
    private Guid UserId => currentUser.UserId!.Value;
}
