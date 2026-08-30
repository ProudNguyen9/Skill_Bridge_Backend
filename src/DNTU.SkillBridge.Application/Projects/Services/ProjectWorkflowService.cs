namespace DNTU.SkillBridge.Application.Projects;

public sealed class ProjectWorkflowService(IProjectRepository projectRepository) : IProjectWorkflowService
{
    public Task<(ProjectSubmitOutcome Outcome, CompanyProjectDetailResponse? Project)> SubmitForApprovalAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        projectRepository.SubmitForApprovalAsync(companyId, projectId, userId, cancellationToken);

    public Task<(ProjectCancelOutcome Outcome, CompanyProjectDetailResponse? Project)> CancelAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        projectRepository.CancelAsync(companyId, projectId, userId, cancellationToken);

    public Task<(ProjectReopenOutcome Outcome, CompanyProjectDetailResponse? Project)> ReopenAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        projectRepository.ReopenAsync(companyId, projectId, userId, cancellationToken);
}
