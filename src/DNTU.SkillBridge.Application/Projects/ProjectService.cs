using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Projects;

namespace DNTU.SkillBridge.Application.Projects;

public interface IProjectService : IProjectRepository;

public sealed class ProjectService(IProjectRepository projectRepository) : IProjectService
{
    private static readonly string[] PublicSorts = ["newest", "oldest", "deadline", "title", "allowance"];

    public static bool IsPublicSortAllowed(string? sort) =>
        string.IsNullOrWhiteSpace(sort) || PublicSorts.Contains(sort.Trim().ToLowerInvariant(), StringComparer.Ordinal);

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

    public Task<(ProjectSubmitOutcome Outcome, CompanyProjectDetailResponse? Project)> SubmitForApprovalAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        projectRepository.SubmitForApprovalAsync(companyId, projectId, userId, cancellationToken);

    public Task<(ProjectCancelOutcome Outcome, CompanyProjectDetailResponse? Project)> CancelAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        projectRepository.CancelAsync(companyId, projectId, userId, cancellationToken);

    public Task<(ProjectReopenOutcome Outcome, CompanyProjectDetailResponse? Project)> ReopenAsync(Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        projectRepository.ReopenAsync(companyId, projectId, userId, cancellationToken);

    public Task<PagedResponse<AdminApprovalListItemResponse>> ListApprovalsAsync(string? status, PageQuery query, CancellationToken cancellationToken) =>
        projectRepository.ListApprovalsAsync(status, query, cancellationToken);

    public Task<AdminApprovalDetailResponse?> GetApprovalDetailAsync(Guid projectId, CancellationToken cancellationToken) =>
        projectRepository.GetApprovalDetailAsync(projectId, cancellationToken);

    public Task<(AdminDecisionOutcome Outcome, AdminApprovalDetailResponse? Detail)> DecideAsync(Guid projectId, Guid adminUserId, ProjectDecision decision, string? note, CancellationToken cancellationToken) =>
        projectRepository.DecideAsync(projectId, adminUserId, decision, note, cancellationToken);

    public Task<PagedResponse<PublicProjectListItemResponse>> SearchPublicProjectsAsync(PublicProjectFilterQuery query, CancellationToken cancellationToken) =>
        projectRepository.SearchPublicProjectsAsync(query, cancellationToken);

    public Task<PublicProjectDetailResponse?> GetPublicProjectBySlugAsync(string slug, CancellationToken cancellationToken) =>
        projectRepository.GetPublicProjectBySlugAsync(slug, cancellationToken);

    public Task<IReadOnlyCollection<PublicProjectSkillResponse>?> ListPublicProjectSkillsAsync(Guid projectId, CancellationToken cancellationToken) =>
        projectRepository.ListPublicProjectSkillsAsync(projectId, cancellationToken);

    public Task<IReadOnlyCollection<PublicProjectListItemResponse>?> ListRelatedProjectsAsync(Guid projectId, int take, CancellationToken cancellationToken) =>
        projectRepository.ListRelatedProjectsAsync(projectId, take, cancellationToken);

    public Task<IReadOnlyCollection<object>> ListPublicProjectMilestonesAsync(Guid projectId, CancellationToken cancellationToken) =>
        projectRepository.ListPublicProjectMilestonesAsync(projectId, cancellationToken);

    public Task<bool> IsPubliclyAccessibleAsync(Guid projectId, CancellationToken cancellationToken) =>
        projectRepository.IsPubliclyAccessibleAsync(projectId, cancellationToken);

    public Task<IReadOnlyCollection<object>?> GetProjectTeamAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken) =>
        projectRepository.GetProjectTeamAsync(companyId, projectId, cancellationToken);

    public Task<CompanyProjectProgressResponse?> GetProjectProgressAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken) =>
        projectRepository.GetProjectProgressAsync(companyId, projectId, cancellationToken);
}
