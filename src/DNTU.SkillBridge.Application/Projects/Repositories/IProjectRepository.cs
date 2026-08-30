using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.Projects;

public interface IProjectRepository
{
    Task<PagedResponse<CompanyProjectListItemResponse>> ListCompanyProjectsAsync(Guid companyId, PageQuery query, string? sort, CancellationToken cancellationToken);
    Task<(ProjectCreateOutcome Outcome, CompanyProjectDetailResponse? Project)> CreateProjectAsync(Guid companyId, CreateProjectRequest request, CancellationToken cancellationToken);
    Task<CompanyProjectDetailResponse?> GetCompanyProjectAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken);
    Task<(ProjectUpdateOutcome Outcome, CompanyProjectDetailResponse? Project)> UpdateProjectAsync(Guid companyId, Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectDeleteOutcome> DeleteProjectAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken);
    Task<(ProjectSubmitOutcome Outcome, CompanyProjectDetailResponse? Project)> SubmitForApprovalAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken);
    Task<(ProjectCancelOutcome Outcome, CompanyProjectDetailResponse? Project)> CancelAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken);
    Task<(ProjectReopenOutcome Outcome, CompanyProjectDetailResponse? Project)> ReopenAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken);
    Task<PagedResponse<AdminApprovalListItemResponse>> ListApprovalsAsync(string? status, PageQuery query, CancellationToken cancellationToken);
    Task<AdminApprovalDetailResponse?> GetApprovalDetailAsync(Guid projectId, CancellationToken cancellationToken);
    Task<(AdminDecisionOutcome Outcome, AdminApprovalDetailResponse? Detail)> DecideAsync(Guid projectId, Guid adminUserId, DNTU.SkillBridge.Domain.Projects.ProjectDecision decision, string? note, CancellationToken cancellationToken);
    Task<PagedResponse<PublicProjectListItemResponse>> SearchPublicProjectsAsync(PublicProjectFilterQuery query, CancellationToken cancellationToken);
    Task<PublicProjectDetailResponse?> GetPublicProjectBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PublicProjectSkillResponse>?> ListPublicProjectSkillsAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PublicProjectListItemResponse>?> ListRelatedProjectsAsync(Guid projectId, int take, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<object>> ListPublicProjectMilestonesAsync(Guid projectId, CancellationToken cancellationToken);
    Task<bool> IsPubliclyAccessibleAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<object>?> GetProjectTeamAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken);
    Task<CompanyProjectProgressResponse?> GetProjectProgressAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken);
}
