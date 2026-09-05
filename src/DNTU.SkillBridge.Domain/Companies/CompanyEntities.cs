using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Companies;

public sealed class Company : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? TaxCode { get; private set; }
    public Guid? IndustryId { get; private set; }
    public string? Website { get; private set; }
    public string? Description { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? Address { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public CompanyVerificationStatus VerificationStatus { get; private set; } = CompanyVerificationStatus.PENDING;
    public DateTimeOffset? VerifiedAt { get; private set; }
    public string? VerificationNote { get; private set; }
    public bool IsActive { get; private set; } = true;
    public ICollection<CompanyMember> Members { get; } = [];

    private Company() { }

    public Company(string name)
    {
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Slug = SlugBuilder.Create(Name);
    }

    /// <summary>Called once at creation to guarantee slug uniqueness.</summary>
    public void AssignSlug(string slug) => Slug = slug;

    public void UpdateProfile(
        string? name,
        string? taxCode,
        Guid? industryId,
        string? website,
        string? description,
        string? logoUrl,
        string? address,
        string? contactEmail,
        string? contactPhone)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
            NormalizedName = Name.ToUpperInvariant();
        }

        TaxCode = string.IsNullOrWhiteSpace(taxCode) ? null : taxCode.Trim();
        IndustryId = industryId;
        Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
        ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim();
    }

    public void AddMember(Guid userId, CompanyMemberRole role, string? title) =>
        Members.Add(new CompanyMember(Id, userId, role, title));

    /// <summary>Admin verification transitions (Task 38 exposes the endpoints; the state lives here).</summary>
    public void MarkVerified(DateTimeOffset at, string? note)
    {
        VerificationStatus = CompanyVerificationStatus.VERIFIED;
        VerifiedAt = at;
        VerificationNote = note;
    }

    public void MarkRejected(DateTimeOffset at, string? note)
    {
        VerificationStatus = CompanyVerificationStatus.REJECTED;
        VerificationNote = note;
    }

    public void MarkSuspended(DateTimeOffset at, string? note)
    {
        VerificationStatus = CompanyVerificationStatus.SUSPENDED;
        VerificationNote = note;
    }

    public void Deactivate() => IsActive = false;

    /// <summary>Later project/payment features must refuse companies that are not verified and active.</summary>
    public bool CanPublishProjects() => IsActive && VerificationStatus == CompanyVerificationStatus.VERIFIED;
}

public sealed class CompanyMember
{
    public Guid CompanyId { get; private set; }
    public Company Company { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public CompanyMemberRole Role { get; private set; }
    public string? Title { get; private set; }

    private CompanyMember() { }

    public CompanyMember(Guid companyId, Guid userId, CompanyMemberRole role, string? title)
    {
        CompanyId = companyId;
        UserId = userId;
        Role = role;
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }

    public void ChangeRole(CompanyMemberRole role) => Role = role;

    public void ChangeTitle(string? title) =>
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
}

public sealed class CompanyInvitation : AuditableEntity
{
    public Guid CompanyId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public CompanyMemberRole Role { get; private set; }
    public CompanyInvitationStatus Status { get; private set; } = CompanyInvitationStatus.PENDING;
    public Guid InvitedByUserId { get; private set; }

    private CompanyInvitation() { }

    public CompanyInvitation(Guid companyId, string email, CompanyMemberRole role, Guid invitedByUserId)
    {
        CompanyId = companyId;
        Email = email.Trim();
        NormalizedEmail = Email.ToUpperInvariant();
        Role = role;
        InvitedByUserId = invitedByUserId;
    }

    public void Revoke() => Status = CompanyInvitationStatus.REVOKED;

    public void Accept() => Status = CompanyInvitationStatus.ACCEPTED;
}

/// <summary>Metadata only; file content upload/download arrives with Task 23.</summary>
public sealed class CompanyDocument : AuditableEntity
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string DocumentType { get; private set; } = string.Empty;
    public string DocumentUrl { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }

    private CompanyDocument() { }

    public CompanyDocument(Guid companyId, string name, string documentType, string documentUrl, Guid uploadedByUserId)
    {
        CompanyId = companyId;
        Name = name.Trim();
        DocumentType = documentType.Trim().ToUpperInvariant();
        DocumentUrl = documentUrl.Trim();
        UploadedByUserId = uploadedByUserId;
    }
}
