using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Teams;

namespace DNTU.SkillBridge.Application.Teams;

public sealed class CreateTeamRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; init; }

    [StringLength(1000)]
    public string? AvatarUrl { get; init; }

    public TeamType TeamType { get; init; } = TeamType.PROJECT;

    public Guid? TargetProjectId { get; init; }

    [StringLength(100)]
    public string? LeaderFunctionalRole { get; init; }
}

public sealed class UpdateTeamRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; init; }

    [StringLength(1000)]
    public string? AvatarUrl { get; init; }

    public TeamType? TeamType { get; init; }

    public Guid? TargetProjectId { get; init; }
}

public sealed class UpdateTeamAvatarRequest
{
    [StringLength(1000)]
    public string? AvatarUrl { get; init; }
}

public sealed class InviteTeamMemberRequest
{
    public Guid? StudentId { get; init; }

    [StringLength(200)]
    public string? Identifier { get; init; }

    public TeamMemberRole Role { get; init; } = TeamMemberRole.MEMBER;

    [StringLength(100)]
    public string? FunctionalRole { get; init; }

    [StringLength(500)]
    public string? Message { get; init; }

    [Range(1, 30)]
    public int ExpiresInDays { get; init; } = 7;
}

public sealed class UpdateMemberFunctionalRoleRequest
{
    [StringLength(100)]
    public string? FunctionalRole { get; init; }
}

public sealed class ChangeTeamLeaderRequest
{
    [Required]
    public Guid StudentId { get; init; }
}

public sealed class RespondRecommendationRequest
{
    public bool Accept { get; init; }

    [StringLength(500)]
    public string? Note { get; init; }
}

public sealed record TeamMemberResponse(
    Guid StudentId,
    TeamMemberRole Role,
    Guid? UserId = null,
    string? DisplayName = null,
    string? StudentCode = null,
    string? Email = null,
    string? AvatarUrl = null,
    string? MajorName = null,
    string? FacultyName = null,
    string? AcademicYear = null,
    string? FunctionalRole = null,
    IReadOnlyCollection<string>? Skills = null,
    DateTimeOffset? JoinedAt = null);

public sealed record TeamResponse(
    Guid Id,
    string Name,
    Guid CreatedByStudentId,
    bool IsLocked,
    IReadOnlyCollection<TeamMemberResponse> Members,
    DateTimeOffset CreatedAt,
    string? Description = null,
    TeamType TeamType = TeamType.PROJECT,
    Guid? TargetProjectId = null,
    string? TargetProjectTitle = null,
    string? LeaderName = null,
    string? AvatarUrl = null,
    Guid? ActiveProjectId = null,
    string? ActiveProjectTitle = null,
    string? ActiveProjectSlug = null,
    string? ActiveProjectStatus = null,
    string? ActiveProjectPhase = null,
    bool HasActiveProject = false);

public sealed record TeamInvitationResponse(
    Guid Id,
    Guid TeamId,
    string TeamName,
    Guid InvitedStudentId,
    Guid InvitedByStudentId,
    TeamMemberRole Role,
    TeamInvitationStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    string? TeamDescription = null,
    TeamType? TeamType = null,
    string? InvitedStudentName = null,
    string? InvitedStudentCode = null,
    string? InvitedStudentEmail = null,
    string? InvitedByName = null,
    string? FunctionalRole = null,
    string? Message = null,
    DateTimeOffset? RespondedAt = null,
    string? TeamAvatarUrl = null);

public sealed record TeamProjectRecommendationResponse(
    Guid Id,
    Guid LecturerId,
    string LecturerName,
    string? LecturerDepartment,
    string? LecturerAcademicTitle,
    Guid TeamId,
    string TeamName,
    Guid ProjectId,
    string ProjectTitle,
    string ProjectSlug,
    string CompanyName,
    string Reason,
    TeamRecommendationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt = null,
    string? ResponseNote = null,
    string? TeamAvatarUrl = null);
