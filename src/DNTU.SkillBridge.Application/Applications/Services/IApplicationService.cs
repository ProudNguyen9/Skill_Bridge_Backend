using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Applications;

namespace DNTU.SkillBridge.Application.Applications;

public interface IApplicationService
{
    Task<(ApplicationApplyOutcome Outcome, ApplicationResponse? Application)> ApplyToProjectAsync(Guid userId, Guid projectId, CreateApplicationRequest request, CancellationToken cancellationToken);
    Task<ApplicationResponse?> GetMyApplicationAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);
    Task<PagedResponse<ApplicationResponse>> ListMyApplicationsAsync(Guid userId, ApplicationListQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ApplicationResponse>> ListMyGroupMemberApplicationsAsync(Guid userId, CancellationToken cancellationToken);
    Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> RespondMemberConsentAsync(Guid userId, Guid applicationId, bool approve, string? note, CancellationToken cancellationToken);
    Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> SubmitTeamApplicationAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);
    Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> ConfirmAdjustmentAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ApplicationConflictItemResponse>> CheckConflictsForProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);
    Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> ResolveConflictAsync(Guid userId, Guid applicationId, ResolveApplicationConflictRequest request, CancellationToken cancellationToken);
    Task<(ApplicationWithdrawOutcome Outcome, ApplicationResponse? Application)> WithdrawMyApplicationAsync(Guid userId, Guid applicationId, string? reason, CancellationToken cancellationToken);
    Task<PagedResponse<CompanyApplicationResponse>> ListCompanyApplicationsAsync(Guid companyId, CompanyApplicationListQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CompanyProjectApplicationSummaryResponse>> GetCompanyApplicationSummaryAsync(Guid companyId, CancellationToken cancellationToken);
    Task<CompanyApplicationResponse?> GetCompanyApplicationAsync(Guid companyId, Guid applicationId, CancellationToken cancellationToken);
    Task<(ApplicationWithdrawOutcome Outcome, CompanyApplicationResponse? Application)> DecideCompanyApplicationAsync(Guid companyId, Guid companyUserId, Guid applicationId, ApplicationStatus targetStatus, ApplicationDecisionRequest? request, CancellationToken cancellationToken);
    Task<(ApplicationActionOutcome Outcome, CompanyApplicationResponse? Application)> RequestAdjustmentAsync(Guid companyId, Guid companyUserId, Guid applicationId, RequestAdjustmentRequest request, CancellationToken cancellationToken);
    Task<(ApplicationActionOutcome Outcome, IReadOnlyCollection<CompanyApplicationResponse> Applications)> MergeIndividualApplicationsAsync(Guid companyId, Guid companyUserId, Guid projectId, MergeIndividualApplicationsRequest request, CancellationToken cancellationToken);
    Task<(ApplicationActionOutcome Outcome, CompanyApplicationResponse? Application)> RequestJoinTeamAsync(Guid companyId, Guid companyUserId, Guid applicationId, RequestJoinTeamRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ApplicationResponse>> ListLeaderJoinRequestsAsync(Guid userId, CancellationToken cancellationToken);
    Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> RespondLeaderJoinApprovalAsync(Guid userId, Guid applicationId, bool approve, string? note, CancellationToken cancellationToken);
}
