using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Lecturers;

namespace DNTU.SkillBridge.Application.Lecturers;

/// <summary>Private profile projection for the authenticated lecturer.</summary>
public sealed record LecturerProfileResponse(
    Guid Id,
    string? LecturerCode,
    string? Department,
    string? AcademicTitle,
    string? Bio,
    string? WebsiteUrl,
    string? OfficeLocation,
    string? PhoneNumber,
    bool IsProfilePublic,
    bool ShowContactInfo,
    bool IsActive,
    string? AvatarUrl = null);

public sealed class UpdateLecturerProfileRequest
{
    [StringLength(200)]
    public string? DisplayName { get; init; }

    [StringLength(32)]
    public string? LecturerCode { get; init; }

    [StringLength(200)]
    public string? Department { get; init; }

    [StringLength(100)]
    public string? AcademicTitle { get; init; }

    [StringLength(1000)]
    public string? Bio { get; init; }

    [Url, StringLength(500)]
    public string? WebsiteUrl { get; init; }

    [StringLength(200)]
    public string? OfficeLocation { get; init; }

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; init; }

    [StringLength(500)]
    public string? AvatarUrl { get; init; }

    public bool IsProfilePublic { get; init; }

    public bool ShowContactInfo { get; init; }
}

/// <summary>Supervision assignment projection; Role and Status use the uppercase wire values (PRIMARY/SUPERVISOR, INVITED/ACTIVE/ENDED).</summary>
public sealed record LecturerAssignmentResponse(
    Guid Id,
    Guid LecturerId,
    Guid ProjectId,
    string Role,
    string Status,
    DateTimeOffset AssignedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? EndedAt,
    string? Note,
    Guid? TeamId = null,
    string? TeamName = null,
    Guid? InvitedByUserId = null,
    string? InvitedByName = null,
    string? ProjectTitle = null,
    string? ProjectSlug = null,
    string? ProjectStatus = null,
    string? CompanyName = null,
    string? LecturerName = null,
    string? LecturerEmail = null,
    string? LecturerDepartment = null,
    string? LecturerAcademicTitle = null,
    string? ExpectedWork = null,
    string? ExpectedDuration = null,
    long? AllowanceShareAmount = null,
    bool IsCourseLinked = false,
    string? CourseName = null,
    DateTimeOffset? RespondedAt = null,
    string? DeclineReason = null,
    int MemberCount = 0,
    string? TeamAvatarUrl = null,
    string? LecturerAvatarUrl = null,
    string? CompanyLogoUrl = null,
    IReadOnlyCollection<string>? ProjectIllustrationUrls = null,
    Guid? LecturerUserId = null);

public sealed class CreateLecturerAssignmentRequest
{
    [Required]
    public Guid ProjectId { get; init; }

    [Required]
    public LecturerAssignmentRole Role { get; init; } = LecturerAssignmentRole.SUPERVISOR;

    [StringLength(1000)]
    public string? Note { get; init; }
}

public sealed class InviteProjectLecturerRequest
{
    [Required]
    public Guid LecturerId { get; init; }

    public Guid? TeamId { get; init; }

    public LecturerAssignmentRole Role { get; init; } = LecturerAssignmentRole.PRIMARY;

    [Required, StringLength(2000, MinimumLength = 5)]
    public string ExpectedWork { get; init; } = string.Empty;

    [StringLength(200)]
    public string? ExpectedDuration { get; init; }

    [Range(0, 1_000_000_000)]
    public long? AllowanceShareAmount { get; init; }

    public bool IsCourseLinked { get; init; }

    [StringLength(200)]
    public string? CourseName { get; init; }

    [StringLength(1000)]
    public string? Note { get; init; }
}

public sealed class DeclineLecturerAssignmentRequest
{
    [StringLength(1000)]
    public string? Reason { get; init; }
}

public sealed record LecturerDirectoryItemResponse(
    Guid LecturerId,
    Guid UserId,
    string DisplayName,
    string Email,
    string? LecturerCode,
    string? Department,
    string? AcademicTitle,
    string? Bio,
    string? OfficeLocation,
    bool IsActive,
    string? AvatarUrl = null);

public sealed record LecturerTeamItemResponse(
    Guid TeamId,
    string TeamName,
    string? Description,
    string TeamType,
    string LeaderName,
    string? LeaderStudentCode,
    int MemberCount,
    IReadOnlyCollection<string> MemberNames,
    string? TeamAvatarUrl = null,
    bool IsBusy = false,
    string? BusyReason = null,
    string? CurrentProjectTitle = null);

public sealed class CreateTeamRecommendationRequest
{
    [Required]
    public Guid TeamId { get; init; }

    [Required]
    public Guid ProjectId { get; init; }

    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; init; } = string.Empty;
}
