using DNTU.SkillBridge.Domain.Companies;

namespace DNTU.SkillBridge.Application.Companies;

public interface ICompanyRepository
{
    Task<Guid?> ResolveCompanyIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<(Guid? CompanyId, CompanyMemberRole? Role)> ResolveCompanyAndRoleAsync(Guid userId, CancellationToken cancellationToken);
    Task<CompanyMember?> FindMemberForUpdateAsync(Guid userId, CancellationToken cancellationToken);
    Task<Company> FindCompanyForUpdateAsync(Guid companyId, CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);
    void AddCompany(Company company);
    Task<CompanyProfileResponse> ProjectCompanyAsync(Guid companyId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CompanyMemberResponse>> ListMembersAsync(Guid companyId, CancellationToken cancellationToken);
    Task<bool> HasMemberWithEmailAsync(Guid companyId, string normalizedEmail, CancellationToken cancellationToken);
    void AddInvitation(CompanyInvitation invitation);
    Task<CompanyMember?> FindMemberForUpdateAsync(Guid companyId, Guid userId, CancellationToken cancellationToken);
    Task<int> CountOwnersAsync(Guid companyId, CancellationToken cancellationToken);
    void RemoveMember(CompanyMember member);
    Task<IReadOnlyCollection<CompanyDocumentResponse>> ListDocumentsAsync(Guid companyId, CancellationToken cancellationToken);
    void AddDocument(CompanyDocument document);
    Task<CompanyDocument?> FindDocumentForUpdateAsync(Guid companyId, Guid documentId, CancellationToken cancellationToken);
    void RemoveDocument(CompanyDocument document);
    Task<IReadOnlyCollection<PublicCompanyResponse>> ListPublicCompaniesAsync(CancellationToken cancellationToken);
    Task<Guid?> FindPublicCompanyIdBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<PublicCompanyResponse?> ProjectPublicCompanyAsync(Guid companyId, CancellationToken cancellationToken);
}
