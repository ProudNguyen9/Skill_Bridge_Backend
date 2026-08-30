using DNTU.SkillBridge.Application.Companies;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

public sealed class CompanyRepository(AppDbContext dbContext) : ICompanyRepository
{
    public async Task<Guid?> ResolveCompanyIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.CompanyMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .Select(member => (Guid?)member.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<(Guid? CompanyId, CompanyMemberRole? Role)> ResolveCompanyAndRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var membership = await dbContext.CompanyMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(member => member.UserId == userId, cancellationToken);
        return membership is null ? (null, null) : (membership.CompanyId, membership.Role);
    }

    public Task<CompanyMember?> FindMemberForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.CompanyMembers
            .AsTracking()
            .SingleOrDefaultAsync(member => member.UserId == userId, cancellationToken);

    public Task<Company> FindCompanyForUpdateAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Companies
            .AsTracking()
            .SingleAsync(item => item.Id == companyId, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        dbContext.Companies.AnyAsync(item => item.Slug == slug, cancellationToken);

    public void AddCompany(Company company) => dbContext.Companies.Add(company);

    public async Task<CompanyProfileResponse> ProjectCompanyAsync(Guid companyId, CancellationToken cancellationToken)
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

    public async Task<IReadOnlyCollection<CompanyMemberResponse>> ListMembersAsync(Guid companyId, CancellationToken cancellationToken)
    {
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

    public Task<bool> HasMemberWithEmailAsync(Guid companyId, string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.CompanyMembers
            .Join(dbContext.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) => new { member, user })
            .AnyAsync(item => item.member.CompanyId == companyId && item.user.NormalizedEmail == normalizedEmail, cancellationToken);

    public void AddInvitation(CompanyInvitation invitation) => dbContext.CompanyInvitations.Add(invitation);

    public Task<CompanyMember?> FindMemberForUpdateAsync(Guid companyId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.CompanyMembers
            .AsTracking()
            .SingleOrDefaultAsync(member => member.CompanyId == companyId && member.UserId == userId, cancellationToken);

    public Task<int> CountOwnersAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.CompanyMembers
            .CountAsync(member => member.CompanyId == companyId && member.Role == CompanyMemberRole.OWNER, cancellationToken);

    public void RemoveMember(CompanyMember member) => dbContext.CompanyMembers.Remove(member);

    public async Task<IReadOnlyCollection<CompanyDocumentResponse>> ListDocumentsAsync(Guid companyId, CancellationToken cancellationToken) =>
        await dbContext.CompanyDocuments
            .AsNoTracking()
            .Where(document => document.CompanyId == companyId)
            .OrderByDescending(document => document.CreatedAt)
            .Select(document => new CompanyDocumentResponse(document.Id, document.Name, document.DocumentType, document.DocumentUrl, document.CreatedAt))
            .ToListAsync(cancellationToken);

    public void AddDocument(CompanyDocument document) => dbContext.CompanyDocuments.Add(document);

    public Task<CompanyDocument?> FindDocumentForUpdateAsync(Guid companyId, Guid documentId, CancellationToken cancellationToken) =>
        dbContext.CompanyDocuments
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == documentId && item.CompanyId == companyId, cancellationToken);

    public void RemoveDocument(CompanyDocument document) => dbContext.CompanyDocuments.Remove(document);

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

        var industryNames = await ResolveIndustryNamesAsync(rows.Select(row => row.IndustryId), cancellationToken);
        return rows
            .Select(row => new PublicCompanyResponse(row.Id, row.Name, row.Slug, row.IndustryId, row.IndustryId.HasValue ? industryNames[row.IndustryId.Value] : null, row.Website, row.Description, row.LogoUrl, row.Address))
            .ToList();
    }

    public async Task<Guid?> FindPublicCompanyIdBySlugAsync(string slug, CancellationToken cancellationToken) =>
        await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.Slug == slug && company.IsActive && company.VerificationStatus == CompanyVerificationStatus.VERIFIED)
            .Select(company => (Guid?)company.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PublicCompanyResponse?> ProjectPublicCompanyAsync(Guid companyId, CancellationToken cancellationToken)
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

    private async Task<Dictionary<Guid, string>> ResolveIndustryNamesAsync(IEnumerable<Guid?> industryIds, CancellationToken cancellationToken)
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
