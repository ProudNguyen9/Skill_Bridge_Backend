using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Students;

namespace DNTU.SkillBridge.Application.Students;

/// <summary>Data operations for student saved projects, project visibility, and skill requirements.</summary>
public interface ISavedProjectRepository
{
    Task<Guid?> FindStudentProfileIdAsync(Guid userId, CancellationToken cancellationToken);

    void AddStudentProfile(StudentProfile profile);

    /// <summary>Reads a project's active flag and status; null when the project does not exist.</summary>
    Task<(bool IsActive, ProjectStatus Status)?> FindProjectVisibilityAsync(Guid projectId, CancellationToken cancellationToken);

    Task<bool> IsProjectPubliclyVisibleAsync(Guid projectId, CancellationToken cancellationToken);

    Task<bool> HasSavedAsync(Guid studentId, Guid projectId, CancellationToken cancellationToken);

    void AddSavedProject(SavedProject savedProject);

    /// <summary>Reads a saved-project bookmark with the context's default tracking behavior.</summary>
    Task<SavedProject?> FindSavedAsync(Guid studentId, Guid projectId, CancellationToken cancellationToken);

    void RemoveSavedProject(SavedProject savedProject);

    /// <summary>Lists one page of the student's saved publicly visible projects, newest save first, with the unfiltered total.</summary>
    Task<(IReadOnlyCollection<SavedProjectListItemResponse> Items, int TotalItems)> ListSavedProjectsAsync(Guid studentId, PageQuery query, CancellationToken cancellationToken);

    /// <summary>Lists a project's required skills as (skill id, code, name) rows.</summary>
    Task<IReadOnlyCollection<(Guid SkillId, string Code, string Name)>> ListRequiredSkillsAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Lists the skill ids the student has declared among active skills.</summary>
    Task<IReadOnlyCollection<Guid>> ListDeclaredActiveSkillIdsAsync(Guid studentId, CancellationToken cancellationToken);
}
