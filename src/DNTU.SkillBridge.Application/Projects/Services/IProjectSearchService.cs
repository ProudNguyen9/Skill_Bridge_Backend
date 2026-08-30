using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.Projects;

public interface IProjectSearchService
{
    Task<PagedResponse<PublicProjectListItemResponse>> SearchPublicProjectsAsync(PublicProjectFilterQuery query, CancellationToken cancellationToken);
    Task<PublicProjectDetailResponse?> GetPublicProjectBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PublicProjectSkillResponse>?> ListPublicProjectSkillsAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PublicProjectListItemResponse>?> ListRelatedProjectsAsync(Guid projectId, int take, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<object>> ListPublicProjectMilestonesAsync(Guid projectId, CancellationToken cancellationToken);
    Task<bool> IsPubliclyAccessibleAsync(Guid projectId, CancellationToken cancellationToken);
}
