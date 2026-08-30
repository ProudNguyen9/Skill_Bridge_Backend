using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Workspaces;

/// <summary>Non-sensitive collaboration preferences for a project workspace.</summary>
public sealed class WorkspaceSettings : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public bool MembersCanCreateTasks { get; private set; } = true;
    public bool MembersCanScheduleMeetings { get; private set; } = true;
    public string? WorkingAgreement { get; private set; }

    private WorkspaceSettings() { }

    public WorkspaceSettings(Guid projectId)
    {
        ProjectId = projectId;
    }

    public void Update(bool membersCanCreateTasks, bool membersCanScheduleMeetings, string? workingAgreement)
    {
        MembersCanCreateTasks = membersCanCreateTasks;
        MembersCanScheduleMeetings = membersCanScheduleMeetings;
        WorkingAgreement = string.IsNullOrWhiteSpace(workingAgreement) ? null : workingAgreement.Trim();
    }
}
