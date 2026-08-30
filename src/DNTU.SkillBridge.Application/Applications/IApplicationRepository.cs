using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Applications;

namespace DNTU.SkillBridge.Application.Applications;

public interface IApplicationRepository
{
    Task<(ApplicationApplyOutcome Outcome, ApplicationResponse? Application)> ApplyToProjectAsync(Guid userId, Guid projectId, CreateApplicationRequest request, CancellationToken cancellationToken);
    Task<ApplicationResponse?> GetMyApplicationAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);
    Task<PagedResponse<ApplicationResponse>> ListMyApplicationsAsync(Guid userId, ApplicationListQuery query, CancellationToken cancellationToken);
    Task<(ApplicationWithdrawOutcome Outcome, ApplicationResponse? Application)> WithdrawMyApplicationAsync(Guid userId, Guid applicationId, string? reason, CancellationToken cancellationToken);
    Task<PagedResponse<CompanyApplicationResponse>> ListCompanyApplicationsAsync(Guid companyId, CompanyApplicationListQuery query, CancellationToken cancellationToken);
    Task<CompanyApplicationResponse?> GetCompanyApplicationAsync(Guid companyId, Guid applicationId, CancellationToken cancellationToken);
    Task<(ApplicationWithdrawOutcome Outcome, CompanyApplicationResponse? Application)> DecideCompanyApplicationAsync(Guid companyId, Guid companyUserId, Guid applicationId, ApplicationStatus targetStatus, string? reason, CancellationToken cancellationToken);
}
