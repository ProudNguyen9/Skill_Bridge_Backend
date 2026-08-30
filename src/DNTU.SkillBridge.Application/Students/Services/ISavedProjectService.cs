using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Students;

namespace DNTU.SkillBridge.Application.Students;

public interface ISavedProjectService
{
    Task<SavedProjectOutcome> SaveProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> UnsaveProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<PagedResponse<SavedProjectListItemResponse>> ListSavedProjectsAsync(Guid userId, PageQuery query, CancellationToken cancellationToken);

    Task<SkillMatchResponse?> MatchSkillsWithProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);
}
