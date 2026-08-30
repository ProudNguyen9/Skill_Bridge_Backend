using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Students;

namespace DNTU.SkillBridge.Api.Students;

public sealed record StudentProfileResponse(
    Guid Id,
    string? StudentCode,
    Guid? MajorId,
    string? MajorName,
    Guid? FacultyId,
    string? FacultyName,
    string? AcademicYear,
    string? PhoneNumber,
    string? Bio,
    string? GithubUrl,
    string? LinkedinUrl,
    string? PortfolioUrl,
    string? CvUrl,
    StudentPrivacyResponse Privacy,
    int ProfileCompletionPercent);

public sealed record StudentPrivacyResponse(
    bool IsProfilePublic,
    bool ShowContactInfo,
    bool ShowDeclaredSkills,
    bool ShowCertificates);

public sealed class UpdateStudentProfileRequest
{
    [StringLength(32)]
    public string? StudentCode { get; init; }

    public Guid? MajorId { get; init; }

    public Guid? FacultyId { get; init; }

    [StringLength(16)]
    public string? AcademicYear { get; init; }

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; init; }

    [StringLength(1000)]
    public string? Bio { get; init; }

    [Url, StringLength(500)]
    public string? GithubUrl { get; init; }

    [Url, StringLength(500)]
    public string? LinkedinUrl { get; init; }

    [Url, StringLength(500)]
    public string? PortfolioUrl { get; init; }

    [Url, StringLength(500)]
    public string? CvUrl { get; init; }
}

public sealed class UpdateStudentPrivacyRequest
{
    public bool IsProfilePublic { get; init; }

    public bool ShowContactInfo { get; init; }

    public bool ShowDeclaredSkills { get; init; } = true;

    public bool ShowCertificates { get; init; }
}

public sealed record DeclaredSkillResponse(Guid SkillId, string SkillCode, string SkillName, string Category, SkillLevel Level);

public sealed class DeclaredSkillItem
{
    [Required]
    public Guid SkillId { get; init; }

    [Required]
    public SkillLevel Level { get; init; }
}

public sealed class ReplaceDeclaredSkillsRequest
{
    [Required, MaxLength(50)]
    public IReadOnlyCollection<DeclaredSkillItem> Skills { get; init; } = [];
}

public sealed record CertificateResponse(Guid Id, string Name, string Issuer, DateOnly IssueDate, string? CertificateUrl);

public sealed class UpsertCertificateRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public DateOnly IssueDate { get; init; }

    [Url, StringLength(500)]
    public string? CertificateUrl { get; init; }
}

/// <summary>Limited public projection; the CV is never included and contact/skills/certificates follow the privacy flags.</summary>
public sealed record PublicStudentResponse(
    Guid Id,
    string DisplayName,
    Guid? FacultyId,
    string? FacultyName,
    Guid? MajorId,
    string? MajorName,
    string? AcademicYear,
    string? Bio,
    string? GithubUrl,
    string? LinkedinUrl,
    string? PortfolioUrl,
    string? PhoneNumber,
    IReadOnlyCollection<PublicStudentSkillResponse> Skills,
    IReadOnlyCollection<PublicStudentCertificateResponse> Certificates);

public sealed record PublicStudentSkillResponse(Guid SkillId, string SkillName, SkillLevel Level);

public sealed record PublicStudentCertificateResponse(string Name, string Issuer, DateOnly IssueDate);
