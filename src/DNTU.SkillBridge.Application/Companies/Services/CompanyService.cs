using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Companies;

namespace DNTU.SkillBridge.Application.Companies;

public sealed class CompanyService(ICompanyRepository companyRepository, IUnitOfWork unitOfWork) : ICompanyService
{
    /// <summary>Returns the caller's own company, or null when the account is not linked to one yet.</summary>
    public async Task<CompanyProfileResponse?> GetMyCompanyAsync(Guid userId, CancellationToken cancellationToken)
    {
        var companyId = await ResolveCompanyIdAsync(userId, cancellationToken);
        if (!companyId.HasValue)
        {
            return null;
        }

        return await companyRepository.ProjectCompanyAsync(companyId.Value, cancellationToken);
    }

    /// <summary>
    /// Creates the company on first save (creator becomes the single OWNER) or updates the existing profile.
    /// Company identity always derives from the authenticated user; request bodies never carry a companyId.
    /// </summary>
    public async Task<(CompanyUpdateOutcome Outcome, CompanyProfileResponse? Profile)> CreateOrUpdateMyCompanyAsync(
        Guid userId, UpdateCompanyProfileRequest request, CancellationToken cancellationToken)
    {
        var membership = await companyRepository.FindMemberForUpdateAsync(userId, cancellationToken);

        Company company;
        if (membership is null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return (CompanyUpdateOutcome.NameRequired, null);
            }

            company = new Company(request.Name);
            await AssignUniqueSlugAsync(company, cancellationToken);
            company.UpdateProfile(request.Name, request.TaxCode, request.IndustryId, request.Website, request.Description, request.LogoUrl, request.Address, request.ContactEmail, request.ContactPhone);
            company.AddMember(userId, CompanyMemberRole.OWNER, "Founder");
            companyRepository.AddCompany(company);
        }
        else
        {
            company = await companyRepository.FindCompanyForUpdateAsync(membership.CompanyId, cancellationToken);
            company.UpdateProfile(request.Name, request.TaxCode, request.IndustryId, request.Website, request.Description, request.LogoUrl, request.Address, request.ContactEmail, request.ContactPhone);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceException)
        {
            // Unique slug/normalized-name/tax-code indexes are the final authority.
            return (CompanyUpdateOutcome.Conflict, null);
        }

        return (membership is null ? CompanyUpdateOutcome.Created : CompanyUpdateOutcome.Updated,
            await companyRepository.ProjectCompanyAsync(company.Id, cancellationToken));
    }

    public async Task<IReadOnlyCollection<CompanyMemberResponse>?> GetMyMembersAsync(Guid userId, CancellationToken cancellationToken)
    {
        var companyId = await ResolveCompanyIdAsync(userId, cancellationToken);
        if (!companyId.HasValue)
        {
            return null;
        }

        return await companyRepository.ListMembersAsync(companyId.Value, cancellationToken);
    }

    /// <summary>Creates a membership invitation. Only owners and managers may invite.</summary>
    public async Task<CompanyInvitationResponse?> InviteMemberAsync(Guid userId, CreateCompanyInvitationRequest request, CancellationToken cancellationToken)
    {
        var (companyId, role) = await companyRepository.ResolveCompanyAndRoleAsync(userId, cancellationToken);
        if (!companyId.HasValue || role is not (CompanyMemberRole.OWNER or CompanyMemberRole.MANAGER))
        {
            return null;
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var alreadyMember = await companyRepository.HasMemberWithEmailAsync(companyId.Value, normalizedEmail, cancellationToken);
        if (alreadyMember)
        {
            return null;
        }

        var invitation = new CompanyInvitation(companyId.Value, request.Email, request.Role, userId);
        companyRepository.AddInvitation(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CompanyInvitationResponse(invitation.Id, invitation.Email, invitation.Role.ToString(), invitation.Status.ToString());
    }

    /// <summary>Removes a member with the minimum-one-owner policy enforced.</summary>
    public async Task<CompanyRemoveMemberOutcome> RemoveMemberAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var (companyId, role) = await companyRepository.ResolveCompanyAndRoleAsync(userId, cancellationToken);
        if (!companyId.HasValue || role is not (CompanyMemberRole.OWNER or CompanyMemberRole.MANAGER))
        {
            return CompanyRemoveMemberOutcome.Forbidden;
        }

        var target = await companyRepository.FindMemberForUpdateAsync(companyId.Value, targetUserId, cancellationToken);
        if (target is null)
        {
            return CompanyRemoveMemberOutcome.NotMember;
        }

        if (target.Role == CompanyMemberRole.OWNER)
        {
            var ownerCount = await companyRepository.CountOwnersAsync(companyId.Value, cancellationToken);
            if (ownerCount <= 1)
            {
                return CompanyRemoveMemberOutcome.LastOwner;
            }
        }

        companyRepository.RemoveMember(target);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CompanyRemoveMemberOutcome.Removed;
    }

    public async Task<IReadOnlyCollection<CompanyDocumentResponse>?> GetMyDocumentsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var companyId = await ResolveCompanyIdAsync(userId, cancellationToken);
        if (!companyId.HasValue)
        {
            return null;
        }

        return await companyRepository.ListDocumentsAsync(companyId.Value, cancellationToken);
    }

    public async Task<CompanyDocumentResponse?> AddMyDocumentAsync(Guid userId, UpsertCompanyDocumentRequest request, CancellationToken cancellationToken)
    {
        var companyId = await ResolveCompanyIdAsync(userId, cancellationToken);
        if (!companyId.HasValue)
        {
            return null;
        }

        var document = new CompanyDocument(companyId.Value, request.Name, request.DocumentType, request.DocumentUrl, userId);
        companyRepository.AddDocument(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CompanyDocumentResponse(document.Id, document.Name, document.DocumentType, document.DocumentUrl, document.CreatedAt);
    }

    /// <summary>Deletes a document owned by the caller's company; other companies' documents answer as not found.</summary>
    public async Task<bool> DeleteMyDocumentAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var companyId = await ResolveCompanyIdAsync(userId, cancellationToken);
        if (!companyId.HasValue)
        {
            return false;
        }

        var document = await companyRepository.FindDocumentForUpdateAsync(companyId.Value, documentId, cancellationToken);
        if (document is null)
        {
            return false;
        }

        companyRepository.RemoveDocument(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Public list of verified, active companies ordered by name.</summary>
    public async Task<IReadOnlyCollection<PublicCompanyResponse>> ListPublicCompaniesAsync(CancellationToken cancellationToken)
    {
        return await companyRepository.ListPublicCompaniesAsync(cancellationToken);
    }

    /// <summary>Public company by slug; unverified or inactive companies answer 404.</summary>
    public async Task<PublicCompanyResponse?> GetPublicCompanyAsync(string slug, CancellationToken cancellationToken)
    {
        var companyId = await companyRepository.FindPublicCompanyIdBySlugAsync(slug, cancellationToken);
        return companyId.HasValue ? await companyRepository.ProjectPublicCompanyAsync(companyId.Value, cancellationToken) : null;
    }

    /// <summary>Public project list per company; projects arrive with Tasks 12/14 and return empty until then.</summary>
    public Task<IReadOnlyCollection<object>> ListPublicCompanyProjectsAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<object>>([]);

    public async Task<Guid?> ResolveCompanyIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await companyRepository.ResolveCompanyIdAsync(userId, cancellationToken);

    private async Task AssignUniqueSlugAsync(Company company, CancellationToken cancellationToken)
    {
        var slug = company.Slug;
        var suffix = 1;
        while (await companyRepository.SlugExistsAsync(slug, cancellationToken))
        {
            slug = $"{company.Slug}-{++suffix}";
        }

        company.AssignSlug(slug);
    }
}
