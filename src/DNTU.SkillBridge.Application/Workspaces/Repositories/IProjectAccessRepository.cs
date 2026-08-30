namespace DNTU.SkillBridge.Application.Workspaces;

/// <summary>
/// Persistence predicates used to resolve the caller's project access. Each method answers
/// a single membership, company-ownership, or lecturer-assignment question.
/// </summary>
public interface IProjectAccessRepository
{
    Task<bool> HasActiveStudentMembershipAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> HasCompanyAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> HasLecturerAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);
}
