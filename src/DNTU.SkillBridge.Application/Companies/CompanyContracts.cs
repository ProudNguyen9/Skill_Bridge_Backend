using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Companies;

namespace DNTU.SkillBridge.Application.Companies;

public sealed record CompanyProfileResponse(
    Guid Id,
    string Name,
    string Slug,
    string? TaxCode,
    Guid? IndustryId,
    string? IndustryName,
    string? Website,
    string? Description,
    string? LogoUrl,
    string? Address,
    string? ContactEmail,
    string? ContactPhone,
    string? Tagline,
    string? CompanyType,
    int? FoundedYear,
    string? BannerUrl,
    string VerificationStatus,
    bool CanPublishProjects);

public sealed class UpdateCompanyProfileRequest
{
    [StringLength(200, MinimumLength = 2)]
    public string? Name { get; init; }

    [StringLength(20)]
    public string? TaxCode { get; init; }

    public Guid? IndustryId { get; init; }

    [StringLength(500)]
    public string? Website { get; init; }

    [StringLength(2000)]
    public string? Description { get; init; }

    [StringLength(500)]
    public string? LogoUrl { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }

    [StringLength(320)]
    public string? ContactEmail { get; init; }

    [StringLength(50)]
    public string? ContactPhone { get; init; }

    [StringLength(200)]
    public string? Tagline { get; init; }

    [StringLength(100)]
    public string? CompanyType { get; init; }

    [Range(1900, 2100)]
    public int? FoundedYear { get; init; }

    [StringLength(500)]
    public string? BannerUrl { get; init; }
}

public sealed record CompanyMemberResponse(Guid UserId, string DisplayName, string Email, string Role, string? Title);

public sealed class CreateCompanyInvitationRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public CompanyMemberRole Role { get; init; } = CompanyMemberRole.MEMBER;

    [StringLength(100)]
    public string? Title { get; init; }
}

public sealed record CompanyInvitationResponse(Guid Id, string Email, string Role, string Status);

public sealed record CompanyDocumentResponse(Guid Id, string Name, string DocumentType, string DocumentUrl, DateTimeOffset CreatedAt);

public sealed class UpsertCompanyDocumentRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(50)]
    public string DocumentType { get; init; } = string.Empty;

    [Required, Url, StringLength(500)]
    public string DocumentUrl { get; init; } = string.Empty;
}

/// <summary>Public company projection: no tax code, private contacts, documents, or member data.</summary>
public sealed record PublicCompanyResponse(
    Guid Id,
    string Name,
    string Slug,
    Guid? IndustryId,
    string? IndustryName,
    string? Website,
    string? Description,
    string? LogoUrl,
    string? Address,
    string? Tagline = null,
    string? CompanyType = null,
    int? FoundedYear = null,
    string? BannerUrl = null,
    string? VerificationStatus = null,
    int OpenProjectsCount = 0);
