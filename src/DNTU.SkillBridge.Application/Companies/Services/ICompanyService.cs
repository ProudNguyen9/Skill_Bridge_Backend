using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Companies;

namespace DNTU.SkillBridge.Application.Companies;

public interface ICompanyService
{
    Task<CompanyProfileResponse?> GetMyCompanyAsync(Guid userId, CancellationToken cancellationToken);
    Task<(CompanyUpdateOutcome Outcome, CompanyProfileResponse? Profile)> CreateOrUpdateMyCompanyAsync(Guid userId, UpdateCompanyProfileRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CompanyMemberResponse>?> GetMyMembersAsync(Guid userId, CancellationToken cancellationToken);
    Task<CompanyInvitationResponse?> InviteMemberAsync(Guid userId, CreateCompanyInvitationRequest request, CancellationToken cancellationToken);
    Task<CompanyRemoveMemberOutcome> RemoveMemberAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CompanyDocumentResponse>?> GetMyDocumentsAsync(Guid userId, CancellationToken cancellationToken);
    Task<CompanyDocumentResponse?> AddMyDocumentAsync(Guid userId, UpsertCompanyDocumentRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteMyDocumentAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PublicCompanyResponse>> ListPublicCompaniesAsync(CancellationToken cancellationToken);
    Task<PublicCompanyResponse?> GetPublicCompanyAsync(string slug, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<object>> ListPublicCompanyProjectsAsync(string slug, CancellationToken cancellationToken);
    Task<Guid?> ResolveCompanyIdAsync(Guid userId, CancellationToken cancellationToken);
}
