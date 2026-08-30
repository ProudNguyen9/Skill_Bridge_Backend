using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

public interface IProjectTaskService
{
    Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> CreateAsync(Guid userId, Guid projectId, CreateProjectTaskRequest request, CancellationToken cancellationToken);

    Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);

    Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> UpdateAsync(Guid userId, Guid taskId, UpdateProjectTaskRequest request, CancellationToken cancellationToken);

    Task<ProjectTaskOutcome> DeleteAsync(Guid userId, Guid taskId, Guid version, CancellationToken cancellationToken);

    Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> MoveAsync(Guid userId, Guid taskId, MoveProjectTaskRequest request, CancellationToken cancellationToken);

    Task<(ProjectTaskOutcome Outcome, ProjectTaskResponse? Task)> AssignAsync(Guid userId, Guid taskId, AssignProjectTaskRequest request, CancellationToken cancellationToken);

    Task<KanbanBoardResponse?> BoardAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);
}
