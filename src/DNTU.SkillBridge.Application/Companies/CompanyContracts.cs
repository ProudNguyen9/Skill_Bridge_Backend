using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Companies;

namespace DNTU.SkillBridge.Application.Companies;

public enum CompanyUpdateOutcome
{
    Created,
    Updated,
    NameRequired,
    Conflict
}

public enum CompanyRemoveMemberOutcome
{
    Removed,
    NotMember,
    LastOwner,
    Forbidden
}

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
    string VerificationStatus,
    bool CanPublishProjects);

public sealed class UpdateCompanyProfileRequest
{
    [StringLength(200, MinimumLength = 2)]
    public string? Name { get; init; }

    [StringLength(20)]
    public string? TaxCode { get; init; }

    public Guid? IndustryId { get; init; }

    [Url, StringLength(500)]
    public string? Website { get; init; }

    [StringLength(2000)]
    public string? Description { get; init; }

    [Url, StringLength(500)]
    public string? LogoUrl { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }

    [EmailAddress, StringLength(320)]
    public string? ContactEmail { get; init; }

    [Phone, StringLength(20)]
    public string? ContactPhone { get; init; }
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
    string? Address);
