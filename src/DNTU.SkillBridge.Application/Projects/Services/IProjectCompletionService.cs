namespace DNTU.SkillBridge.Application.Projects;

public interface IProjectCompletionService
{
    Task<(ProjectCompletionOutcome Outcome, ProjectCompletionResponse? Completion)> CompleteAsync(Guid actorUserId, bool isAdministrator, Guid projectId, CancellationToken cancellationToken);
    Task<ProjectCompletionResponse?> GetAsync(Guid projectId, CancellationToken cancellationToken);
}
