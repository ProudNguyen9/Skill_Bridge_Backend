using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Projects;

namespace DNTU.SkillBridge.Application.Projects;

public interface IProjectService
{
    Task<PagedResponse<CompanyProjectListItemResponse>> ListCompanyProjectsAsync(Guid companyId, PageQuery query, string? sort, CancellationToken cancellationToken);
    Task<(ProjectCreateOutcome Outcome, CompanyProjectDetailResponse? Project)> CreateProjectAsync(Guid companyId, CreateProjectRequest request, CancellationToken cancellationToken);
    Task<CompanyProjectDetailResponse?> GetCompanyProjectAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken);
    Task<(ProjectUpdateOutcome Outcome, CompanyProjectDetailResponse? Project)> UpdateProjectAsync(Guid companyId, Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectDeleteOutcome> DeleteProjectAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken);
}
