using DNTU.SkillBridge.Application.Common;
namespace DNTU.SkillBridge.Application.Projects;

public sealed class ProjectService(IProjectRepository projectRepository) : IProjectService
{
    public Task<PagedResponse<CompanyProjectListItemResponse>> ListCompanyProjectsAsync(Guid companyId, PageQuery query, string? sort, CancellationToken cancellationToken) =>
        projectRepository.ListCompanyProjectsAsync(companyId, query, sort, cancellationToken);

    public Task<(ProjectCreateOutcome Outcome, CompanyProjectDetailResponse? Project)> CreateProjectAsync(Guid companyId, CreateProjectRequest request, CancellationToken cancellationToken) =>
        projectRepository.CreateProjectAsync(companyId, request, cancellationToken);

    public Task<CompanyProjectDetailResponse?> GetCompanyProjectAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken) =>
        projectRepository.GetCompanyProjectAsync(companyId, projectId, cancellationToken);

    public Task<(ProjectUpdateOutcome Outcome, CompanyProjectDetailResponse? Project)> UpdateProjectAsync(Guid companyId, Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken) =>
        projectRepository.UpdateProjectAsync(companyId, projectId, request, cancellationToken);

    public Task<ProjectDeleteOutcome> DeleteProjectAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken) =>
        projectRepository.DeleteProjectAsync(companyId, projectId, cancellationToken);
}
