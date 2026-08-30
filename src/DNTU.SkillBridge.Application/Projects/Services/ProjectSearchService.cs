using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.Projects;

public sealed class ProjectSearchService(IProjectRepository projectRepository) : IProjectSearchService
{
    private static readonly string[] PublicSorts = ["newest", "oldest", "deadline", "title", "allowance"];

    public static bool IsPublicSortAllowed(string? sort) =>
        string.IsNullOrWhiteSpace(sort) || PublicSorts.Contains(sort.Trim().ToLowerInvariant(), StringComparer.Ordinal);

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
}
