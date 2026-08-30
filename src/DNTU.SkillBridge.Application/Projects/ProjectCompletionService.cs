namespace DNTU.SkillBridge.Application.Projects;

public interface IProjectCompletionService : IProjectCompletionRepository;

public sealed class ProjectCompletionService(IProjectCompletionRepository completionRepository) : IProjectCompletionService
{
    public Task<(ProjectCompletionOutcome Outcome, ProjectCompletionResponse? Completion)> CompleteAsync(Guid actorUserId, bool isAdministrator, Guid projectId, CancellationToken cancellationToken) =>
        completionRepository.CompleteAsync(actorUserId, isAdministrator, projectId, cancellationToken);

    public Task<ProjectCompletionResponse?> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
        completionRepository.GetAsync(projectId, cancellationToken);
}
