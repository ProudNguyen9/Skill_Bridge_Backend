using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Companies;

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

public sealed class CompanyService(AppDbContext dbContext)
{
    /// <summary>Returns the caller's own company, or null when the account is not linked to one yet.</summary>
    public async Task<CompanyProfileResponse?> GetMyCompanyAsync(Guid userId, CancellationToken cancellationToken)
    {
        var companyId = await ResolveCompanyIdAsync(userId, cancellationToken);
        if (!companyId.HasValue)
        {
            return null;
        }

        return await ProjectCompanyAsync(dbContext, companyId.Value, cancellationToken);
    }

    /// <summary>
    /// Creates the company on first save (creator becomes the single OWNER) or updates the existing profile.
    /// Company identity always derives from the authenticated user; request bodies never carry a companyId.
    /// </summary>
    public async Task<(CompanyUpdateOutcome Outcome, CompanyProfileResponse? Profile)> CreateOrUpdateMyCompanyAsync(
        Guid userId, UpdateCompanyProfileRequest request, CancellationToken cancellationToken)
    {
        var membership = await dbContext.CompanyMembers
            .AsTracking()
            .SingleOrDefaultAsync(member => member.UserId == userId, cancellationToken);

        Company company;
        if (membership is null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return (CompanyUpdateOutcome.NameRequired, null);
            }

            company = new Company(request.Name);
            await AssignUniqueSlugAsync(dbContext, company, cancellationToken);
            company.UpdateProfile(request.Name, request.TaxCode, request.IndustryId, request.Website, request.Description, request.LogoUrl, request.Address, request.ContactEmail, request.ContactPhone);
            company.AddMember(userId, CompanyMemberRole.OWNER, "Founder");
            dbContext.Companies.Add(company);
        }
        else
        {
            company = await dbContext.Companies
                .AsTracking()
                .SingleAsync(item => item.Id == membership.CompanyId, cancellationToken);
            company.UpdateProfile(request.Name, request.TaxCode, request.IndustryId, request.Website, request.Description, request.LogoUrl, request.Address, request.ContactEmail, request.ContactPhone);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique slug/normalized-name/tax-code indexes are the final authority.
            return (CompanyUpdateOutcome.Conflict, null);
        }

        return (membership is null ? CompanyUpdateOutcome.Created : CompanyUpdateOutcome.Updated,
            await ProjectCompanyAsync(dbContext, company.Id, cancellationToken));
    }

    public async Task<IReadOnlyCollection<CompanyMemberResponse>?> GetMyMembersAsync(Guid userId, CancellationToken cancellationToken)
    {
        var companyId = await ResolveCompanyIdAsync(userId, cancellationToken);
        if (!companyId.HasValue)
        {
            return null;
        }

        var rows = await dbContext.CompanyMembers
            .AsNoTracking()
            .Where(member => member.CompanyId == companyId)
            .Join(dbContext.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) => new { member, user })
            .OrderBy(item => item.member.Role)
            .ThenBy(item => item.user.DisplayName)
            .Select(item => new { item.user.Id, item.user.DisplayName, item.user.Email, item.member.Role, item.member.Title })
            .ToListAsync(cancellationToken);

        return rows
            .Select(item => new CompanyMemberResponse(item.Id, item.DisplayName, item.Email, item.Role.ToString(), item.Title))
            .ToList();
    }

    /// <summary>Creates a membership invitation. Only owners and managers may invite.</summary>
    public async Task<CompanyInvitationResponse?> InviteMemberAsync(Guid userId, CreateCompanyInvitationRequest request, CancellationToken cancellationToken)
    {
        var (companyId, role) = await ResolveCompanyAndRoleAsync(userId, cancellationToken);
        if (!companyId.HasValue || role is not (CompanyMemberRole.OWNER or CompanyMemberRole.MANAGER))
        {
            return null;
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var alreadyMember = await dbContext.CompanyMembers
            .Join(dbContext.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) => new { member, user })
            .AnyAsync(item => item.member.CompanyId == companyId && item.user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (alreadyMember)
        {
            return null;
        }

        var invitation = new CompanyInvitation(companyId.Value, request.Email, request.Role, userId);
        dbContext.CompanyInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CompanyInvitationResponse(invitation.Id, invitation.Email, invitation.Role.ToString(), invitation.Status.ToString());
    }

    /// <summary>Removes a member with the minimum-one-owner policy enforced.</summary>
    public async Task<CompanyRemoveMemberOutcome> RemoveMemberAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var (companyId, role) = await ResolveCompanyAndRoleAsync(userId, cancellationToken);
        if (!companyId.HasValue || role is not (CompanyMemberRole.OWNER or CompanyMemberRole.MANAGER))
        {
            return CompanyRemoveMemberOutcome.Forbidden;
        }

        var target = await dbContext.CompanyMembers
            .AsTracking()
            .SingleOrDefaultAsync(member => member.CompanyId == companyId && member.UserId == targetUserId, cancellationToken);
        if (target is null)
        {
            return CompanyRemoveMemberOutcome.NotMember;
        }

        if (target.Role == CompanyMemberRole.OWNER)
        {
            var ownerCount = await dbContext.CompanyMembers
                .CountAsync(member => member.CompanyId == companyId && member.Role == CompanyMemberRole.OWNER, cancellationToken);
            if (ownerCount <= 1)
            {
                return CompanyRemoveMemberOutcome.LastOwner;
            }
        }

        dbContext.CompanyMembers.Remove(target);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CompanyRemoveMemberOutcome.Removed;
    }

    public async Task<IReadOnlyCollection<CompanyDocumentResponse>?> GetMyDocumentsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var companyId = await ResolveCompanyIdAsync(userId, cancellationToken);
        if (!companyId.HasValue)
        {
            return null;
        }

        return await dbContext.CompanyDocuments
            .AsNoTracking()
            .Where(document => document.CompanyId == companyId)
            .OrderByDescending(document => document.CreatedAt)
            .Select(document => new CompanyDocumentResponse(document.Id, document.Name, document.DocumentType, document.DocumentUrl, document.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyDocumentResponse?> AddMyDocumentAsync(Guid userId, UpsertCompanyDocumentRequest request, CancellationToken cancellationToken)
    {
        var companyId = await ResolveCompanyIdAsync(userId, cancellationToken);
        if (!companyId.HasValue)
        {
            return null;
        }

        var document = new CompanyDocument(companyId.Value, request.Name, request.DocumentType, request.DocumentUrl, userId);
        dbContext.CompanyDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
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

        var document = await dbContext.CompanyDocuments
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == documentId && item.CompanyId == companyId, cancellationToken);
        if (document is null)
        {
            return false;
        }

        dbContext.CompanyDocuments.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Public list of verified, active companies ordered by name.</summary>
    public async Task<IReadOnlyCollection<PublicCompanyResponse>> ListPublicCompaniesAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.IsActive && company.VerificationStatus == CompanyVerificationStatus.VERIFIED)
            .OrderBy(company => company.Name)
            .Select(company => new
            {
                company.Id,
                company.Name,
                company.Slug,
                company.IndustryId,
                company.Website,
                company.Description,
                company.LogoUrl,
                company.Address
            })
            .ToListAsync(cancellationToken);

        var industryNames = await ResolveIndustryNamesAsync(dbContext, rows.Select(row => row.IndustryId), cancellationToken);
        return rows
            .Select(row => new PublicCompanyResponse(row.Id, row.Name, row.Slug, row.IndustryId, row.IndustryId.HasValue ? industryNames[row.IndustryId.Value] : null, row.Website, row.Description, row.LogoUrl, row.Address))
            .ToList();
    }

    /// <summary>Public company by slug; unverified or inactive companies answer 404.</summary>
    public async Task<PublicCompanyResponse?> GetPublicCompanyAsync(string slug, CancellationToken cancellationToken)
    {
        var companyId = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.Slug == slug && company.IsActive && company.VerificationStatus == CompanyVerificationStatus.VERIFIED)
            .Select(company => (Guid?)company.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return companyId.HasValue ? await ProjectPublicCompanyAsync(dbContext, companyId.Value, cancellationToken) : null;
    }

    /// <summary>Public project list per company; projects arrive with Tasks 12/14 and return empty until then.</summary>
    public Task<IReadOnlyCollection<object>> ListPublicCompanyProjectsAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<object>>([]);

    public async Task<Guid?> ResolveCompanyIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.CompanyMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .Select(member => (Guid?)member.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<(Guid? CompanyId, CompanyMemberRole? Role)> ResolveCompanyAndRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var membership = await dbContext.CompanyMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(member => member.UserId == userId, cancellationToken);
        return membership is null ? (null, null) : (membership.CompanyId, membership.Role);
    }

    private static async Task AssignUniqueSlugAsync(AppDbContext dbContext, Company company, CancellationToken cancellationToken)
    {
        var slug = company.Slug;
        var suffix = 1;
        while (await dbContext.Companies.AnyAsync(item => item.Slug == slug, cancellationToken))
        {
            slug = $"{company.Slug}-{++suffix}";
        }

        company.AssignSlug(slug);
    }

    private static async Task<CompanyProfileResponse> ProjectCompanyAsync(AppDbContext dbContext, Guid companyId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .SingleAsync(item => item.Id == companyId, cancellationToken);
        string? industryName = null;
        if (company.IndustryId.HasValue)
        {
            industryName = await dbContext.Industries
                .Where(industry => industry.Id == company.IndustryId)
                .Select(industry => industry.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new CompanyProfileResponse(
            company.Id,
            company.Name,
            company.Slug,
            company.TaxCode,
            company.IndustryId,
            industryName,
            company.Website,
            company.Description,
            company.LogoUrl,
            company.Address,
            company.ContactEmail,
            company.ContactPhone,
            company.VerificationStatus.ToString(),
            company.CanPublishProjects());
    }

    private static async Task<PublicCompanyResponse?> ProjectPublicCompanyAsync(AppDbContext dbContext, Guid companyId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .SingleAsync(item => item.Id == companyId, cancellationToken);
        string? industryName = null;
        if (company.IndustryId.HasValue)
        {
            industryName = await dbContext.Industries
                .Where(industry => industry.Id == company.IndustryId)
                .Select(industry => industry.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new PublicCompanyResponse(company.Id, company.Name, company.Slug, company.IndustryId, industryName, company.Website, company.Description, company.LogoUrl, company.Address);
    }

    private static async Task<Dictionary<Guid, string>> ResolveIndustryNamesAsync(
        AppDbContext dbContext,
        IEnumerable<Guid?> industryIds,
        CancellationToken cancellationToken)
    {
        var ids = industryIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.Industries
            .Where(industry => ids.Contains(industry.Id))
            .ToDictionaryAsync(industry => industry.Id, industry => industry.Name, cancellationToken);
    }
}
