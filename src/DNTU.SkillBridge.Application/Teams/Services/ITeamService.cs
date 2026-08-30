using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Domain.Teams;

namespace DNTU.SkillBridge.Application.Teams;

public interface ITeamService
{
    Task<TeamResponse> CreateAsync(Guid userId, CreateTeamRequest request, CancellationToken cancellationToken);

    Task<TeamResponse?> GetAsync(Guid userId, Guid teamId, CancellationToken cancellationToken);

    Task<(TeamOperationOutcome Outcome, TeamResponse? Team)> UpdateAsync(Guid userId, Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken);

    Task<TeamOperationOutcome> DeleteAsync(Guid userId, Guid teamId, CancellationToken cancellationToken);

    Task<(TeamOperationOutcome Outcome, TeamInvitationResponse? Invitation)> InviteAsync(Guid userId, Guid teamId, InviteTeamMemberRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TeamInvitationResponse>> ListMyInvitationsAsync(Guid userId, CancellationToken cancellationToken);

    Task<TeamOperationOutcome> RespondAsync(Guid userId, Guid invitationId, bool accept, CancellationToken cancellationToken);

    Task<TeamOperationOutcome> RemoveMemberAsync(Guid userId, Guid teamId, Guid memberStudentId, CancellationToken cancellationToken);

    Task<TeamOperationOutcome> ChangeLeaderAsync(Guid userId, Guid teamId, Guid newLeaderStudentId, CancellationToken cancellationToken);
}
