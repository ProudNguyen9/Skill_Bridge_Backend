using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Applications;

namespace DNTU.SkillBridge.Application.Applications;

public sealed class ApplicationService(IApplicationRepository applicationRepository) : IApplicationService
{
    public Task<(ApplicationApplyOutcome Outcome, ApplicationResponse? Application)> ApplyToProjectAsync(Guid userId, Guid projectId, CreateApplicationRequest request, CancellationToken cancellationToken) =>
        applicationRepository.ApplyToProjectAsync(userId, projectId, request, cancellationToken);

    public Task<ApplicationResponse?> GetMyApplicationAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken) =>
        applicationRepository.GetMyApplicationAsync(userId, applicationId, cancellationToken);

    public Task<PagedResponse<ApplicationResponse>> ListMyApplicationsAsync(Guid userId, ApplicationListQuery query, CancellationToken cancellationToken) =>
        applicationRepository.ListMyApplicationsAsync(userId, query, cancellationToken);

    public Task<(ApplicationWithdrawOutcome Outcome, ApplicationResponse? Application)> WithdrawMyApplicationAsync(Guid userId, Guid applicationId, string? reason, CancellationToken cancellationToken) =>
        applicationRepository.WithdrawMyApplicationAsync(userId, applicationId, reason, cancellationToken);

    public Task<PagedResponse<CompanyApplicationResponse>> ListCompanyApplicationsAsync(Guid companyId, CompanyApplicationListQuery query, CancellationToken cancellationToken) =>
        applicationRepository.ListCompanyApplicationsAsync(companyId, query, cancellationToken);

    public Task<CompanyApplicationResponse?> GetCompanyApplicationAsync(Guid companyId, Guid applicationId, CancellationToken cancellationToken) =>
        applicationRepository.GetCompanyApplicationAsync(companyId, applicationId, cancellationToken);

    public Task<(ApplicationWithdrawOutcome Outcome, CompanyApplicationResponse? Application)> DecideCompanyApplicationAsync(Guid companyId, Guid companyUserId, Guid applicationId, ApplicationStatus targetStatus, string? reason, CancellationToken cancellationToken) =>
        applicationRepository.DecideCompanyApplicationAsync(companyId, companyUserId, applicationId, targetStatus, reason, cancellationToken);
}
