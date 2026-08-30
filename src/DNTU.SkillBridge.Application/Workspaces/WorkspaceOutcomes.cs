namespace DNTU.SkillBridge.Application.Workspaces;

public enum ProjectTaskOutcome
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    InvalidAssignee
}

public enum TaskCollaborationOutcome
{
    Success,
    NotFound,
    Forbidden,
    Conflict
}
