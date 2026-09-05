namespace DNTU.SkillBridge.Domain.Projects;

/// <summary>
/// Lifecycle state of a project. Member names intentionally match the uppercase
/// wire values (e.g. PENDING_APPROVAL). Draft-only editing is Task 12; the
/// submit/approve transitions arrive with Task 13.
/// </summary>
public enum ProjectStatus
{
    DRAFT = 1,
    PENDING_APPROVAL = 2,
    CHANGES_REQUESTED = 3,
    APPROVED = 4,
    RECRUITING = 5,
    IN_PROGRESS = 6,
    COMPLETED = 7,
    CANCELLED = 8,
    REJECTED = 9,
    SUSPENDED = 10
}

public enum RequirementLevel
{
    NICE_TO_HAVE = 1,
    IMPORTANT = 2,
    MUST_HAVE = 3
}

/// <summary>
/// Append-only workflow decisions recorded against a project. The current state lives on
/// the Project entity itself; this table never gets rows updated or deleted so the full
/// decision history (who, when, what, why) is preserved.
/// </summary>
public enum ProjectDecision
{
    SUBMITTED = 1,
    APPROVED = 2,
    CHANGES_REQUESTED = 3,
    REJECTED = 4,
    SUSPENDED = 5,
    RESUMED = 6,
    CANCELLED = 7,
    REOPENED = 8
}
