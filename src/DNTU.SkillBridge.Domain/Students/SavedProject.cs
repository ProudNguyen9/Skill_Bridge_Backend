using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Students;

/// <summary>
/// A student's bookmark of a project. The composite key (StudentId, ProjectId) makes
/// saving idempotent; visibility rules are enforced by the service, not here.
/// </summary>
public sealed class SavedProject : AuditableEntity
{
    public Guid StudentId { get; private set; }
    public Guid ProjectId { get; private set; }

    private SavedProject() { }

    public SavedProject(Guid studentId, Guid projectId)
    {
        StudentId = studentId;
        ProjectId = projectId;
    }
}
