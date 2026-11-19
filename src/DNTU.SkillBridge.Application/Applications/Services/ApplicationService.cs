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

    public Task<IReadOnlyCollection<ApplicationResponse>> ListMyGroupMemberApplicationsAsync(Guid userId, CancellationToken cancellationToken) =>
        applicationRepository.ListMyGroupMemberApplicationsAsync(userId, cancellationToken);

    public Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> RespondMemberConsentAsync(Guid userId, Guid applicationId, bool approve, string? note, CancellationToken cancellationToken) =>
        applicationRepository.RespondMemberConsentAsync(userId, applicationId, approve, note, cancellationToken);

    public Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> SubmitTeamApplicationAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken) =>
        applicationRepository.SubmitTeamApplicationAsync(userId, applicationId, cancellationToken);

    public Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> ConfirmAdjustmentAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken) =>
        applicationRepository.ConfirmAdjustmentAsync(userId, applicationId, cancellationToken);

    public Task<IReadOnlyCollection<ApplicationConflictItemResponse>> CheckConflictsForProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        applicationRepository.CheckConflictsForProjectAsync(userId, projectId, cancellationToken);

    public Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> ResolveConflictAsync(Guid userId, Guid applicationId, ResolveApplicationConflictRequest request, CancellationToken cancellationToken) =>
        applicationRepository.ResolveConflictAsync(userId, applicationId, request, cancellationToken);

    public Task<(ApplicationWithdrawOutcome Outcome, ApplicationResponse? Application)> WithdrawMyApplicationAsync(Guid userId, Guid applicationId, string? reason, CancellationToken cancellationToken) =>
        applicationRepository.WithdrawMyApplicationAsync(userId, applicationId, reason, cancellationToken);

    public Task<PagedResponse<CompanyApplicationResponse>> ListCompanyApplicationsAsync(Guid companyId, CompanyApplicationListQuery query, CancellationToken cancellationToken) =>
        applicationRepository.ListCompanyApplicationsAsync(companyId, query, cancellationToken);

    public Task<IReadOnlyCollection<CompanyProjectApplicationSummaryResponse>> GetCompanyApplicationSummaryAsync(Guid companyId, CancellationToken cancellationToken) =>
        applicationRepository.GetCompanyApplicationSummaryAsync(companyId, cancellationToken);

    public Task<CompanyApplicationResponse?> GetCompanyApplicationAsync(Guid companyId, Guid applicationId, CancellationToken cancellationToken) =>
        applicationRepository.GetCompanyApplicationAsync(companyId, applicationId, cancellationToken);

    public Task<(ApplicationWithdrawOutcome Outcome, CompanyApplicationResponse? Application)> DecideCompanyApplicationAsync(Guid companyId, Guid companyUserId, Guid applicationId, ApplicationStatus targetStatus, ApplicationDecisionRequest? request, CancellationToken cancellationToken) =>
        applicationRepository.DecideCompanyApplicationAsync(companyId, companyUserId, applicationId, targetStatus, request, cancellationToken);

    public Task<(ApplicationActionOutcome Outcome, CompanyApplicationResponse? Application)> RequestAdjustmentAsync(Guid companyId, Guid companyUserId, Guid applicationId, RequestAdjustmentRequest request, CancellationToken cancellationToken) =>
        applicationRepository.RequestAdjustmentAsync(companyId, companyUserId, applicationId, request, cancellationToken);

    public Task<(ApplicationActionOutcome Outcome, IReadOnlyCollection<CompanyApplicationResponse> Applications)> MergeIndividualApplicationsAsync(Guid companyId, Guid companyUserId, Guid projectId, MergeIndividualApplicationsRequest request, CancellationToken cancellationToken) =>
        applicationRepository.MergeIndividualApplicationsAsync(companyId, companyUserId, projectId, request, cancellationToken);

    public Task<(ApplicationActionOutcome Outcome, CompanyApplicationResponse? Application)> RequestJoinTeamAsync(Guid companyId, Guid companyUserId, Guid applicationId, RequestJoinTeamRequest request, CancellationToken cancellationToken) =>
        applicationRepository.RequestJoinTeamAsync(companyId, companyUserId, applicationId, request, cancellationToken);

    public Task<IReadOnlyCollection<ApplicationResponse>> ListLeaderJoinRequestsAsync(Guid userId, CancellationToken cancellationToken) =>
        applicationRepository.ListLeaderJoinRequestsAsync(userId, cancellationToken);

    public Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> RespondLeaderJoinApprovalAsync(Guid userId, Guid applicationId, bool approve, string? note, CancellationToken cancellationToken) =>
        applicationRepository.RespondLeaderJoinApprovalAsync(userId, applicationId, approve, note, cancellationToken);
}
