namespace DNTU.SkillBridge.Domain.Workspaces;

public enum ProjectMemberRole
{
    STUDENT = 1,
    COMPANY = 2,
    LECTURER = 3
}

public enum ProjectRiskLevel
{
    LOW = 1,
    MEDIUM = 2,
    HIGH = 3
}

public enum ProjectTaskStatus
{
    BACKLOG = 1,
    TODO = 2,
    IN_PROGRESS = 3,
    REVIEW = 4,
    DONE = 5
}

public enum ProjectTaskPriority
{
    LOW = 1,
    MEDIUM = 2,
    HIGH = 3,
    URGENT = 4
}
