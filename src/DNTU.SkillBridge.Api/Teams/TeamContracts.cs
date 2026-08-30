using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Teams;

namespace DNTU.SkillBridge.Api.Teams;

public sealed class CreateTeamRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateTeamRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;
}

public sealed class InviteTeamMemberRequest
{
    [Required]
    public Guid StudentId { get; init; }

    public TeamMemberRole Role { get; init; } = TeamMemberRole.MEMBER;

    [Range(1, 30)]
    public int ExpiresInDays { get; init; } = 7;
}

public sealed class ChangeTeamLeaderRequest
{
    [Required]
    public Guid StudentId { get; init; }
}

public sealed record TeamMemberResponse(Guid StudentId, TeamMemberRole Role);

public sealed record TeamResponse(Guid Id, string Name, Guid CreatedByStudentId, bool IsLocked, IReadOnlyCollection<TeamMemberResponse> Members, DateTimeOffset CreatedAt);

public sealed record TeamInvitationResponse(
    Guid Id,
    Guid TeamId,
    string TeamName,
    Guid InvitedStudentId,
    Guid InvitedByStudentId,
    TeamMemberRole Role,
    TeamInvitationStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);
