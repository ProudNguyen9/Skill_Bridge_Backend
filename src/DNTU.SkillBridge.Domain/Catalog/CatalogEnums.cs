namespace DNTU.SkillBridge.Domain.Catalog;

/// <summary>
/// Enum member names intentionally match the uppercase wire values exposed by the
/// catalog metadata endpoints and persisted in PostgreSQL (e.g. IN_PROGRESS).
/// </summary>
public enum SkillCategory
{
    TECHNICAL = 1,
    ENGINEERING = 2,
    COLLABORATION = 3,
    BUSINESS = 4
}

public enum ProjectDifficulty
{
    BEGINNER = 1,
    INTERMEDIATE = 2,
    ADVANCED = 3
}

public enum ProjectWorkType
{
    ONSITE = 1,
    HYBRID = 2,
    REMOTE = 3
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
    CRITICAL = 4
}
