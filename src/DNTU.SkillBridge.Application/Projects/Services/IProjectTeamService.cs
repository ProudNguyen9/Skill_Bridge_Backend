namespace DNTU.SkillBridge.Application.Projects;

public interface IProjectTeamService
{
    Task<IReadOnlyCollection<object>?> GetProjectTeamAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken);
    Task<CompanyProjectProgressResponse?> GetProjectProgressAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken);
}
