using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Projects;

namespace DNTU.SkillBridge.Application.Projects;

public interface IProjectWorkflowService
{
    Task<(ProjectSubmitOutcome Outcome, CompanyProjectDetailResponse? Project)> SubmitForApprovalAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken);
    Task<(ProjectCancelOutcome Outcome, CompanyProjectDetailResponse? Project)> CancelAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken);
    Task<(ProjectReopenOutcome Outcome, CompanyProjectDetailResponse? Project)> ReopenAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken);
}
