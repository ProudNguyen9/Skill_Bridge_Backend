namespace DNTU.SkillBridge.Application.Projects;

public sealed class ProjectTeamService(IProjectRepository projectRepository) : IProjectTeamService
{
    public Task<IReadOnlyCollection<object>?> GetProjectTeamAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken) =>
        projectRepository.GetProjectTeamAsync(companyId, projectId, cancellationToken);

    public Task<CompanyProjectProgressResponse?> GetProjectProgressAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken) =>
        projectRepository.GetProjectProgressAsync(companyId, projectId, cancellationToken);
}
