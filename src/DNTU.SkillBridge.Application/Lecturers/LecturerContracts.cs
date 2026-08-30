using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Lecturers;

namespace DNTU.SkillBridge.Application.Lecturers;

public enum LecturerAssignmentCreateOutcome
{
    Created,
    LecturerNotFound,
    LecturerInactive,
    DuplicateAssignment
}

public enum LecturerAssignmentAcceptOutcome
{
    Accepted,
    NotFound,
    NotOpenForAcceptance
}

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
    bool IsActive);

public sealed class UpdateLecturerProfileRequest
{
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
    string? Note);

public sealed class CreateLecturerAssignmentRequest
{
    [Required]
    public Guid ProjectId { get; init; }

    [Required]
    public LecturerAssignmentRole Role { get; init; } = LecturerAssignmentRole.SUPERVISOR;

    [StringLength(1000)]
    public string? Note { get; init; }
}
