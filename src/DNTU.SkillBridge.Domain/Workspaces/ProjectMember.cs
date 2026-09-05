using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Workspaces;

/// <summary>Authoritative active-project membership created after commitment confirmation.</summary>
public sealed class ProjectMember : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public Guid StudentId { get; private set; }
    public DNTU.SkillBridge.Domain.Students.StudentProfile Student { get; private set; } = null!;
    public ProjectMemberRole Role { get; private set; } = ProjectMemberRole.STUDENT;
    public bool IsActive { get; private set; } = true;

    private ProjectMember() { }

    public ProjectMember(Guid projectId, Guid studentId)
    {
        ProjectId = projectId;
        StudentId = studentId;
    }

    public void Deactivate() => IsActive = false;
}
