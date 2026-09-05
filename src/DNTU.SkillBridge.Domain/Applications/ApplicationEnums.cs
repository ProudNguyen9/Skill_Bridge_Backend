namespace DNTU.SkillBridge.Domain.Applications;

/// <summary>
/// Lifecycle state of a student application. Member names intentionally match the
/// uppercase wire values. This task (16) only ever writes PENDING and WITHDRAWN;
/// SHORTLISTED/ACCEPTED/REJECTED are reserved for company review (Task 18).
/// </summary>
public enum ApplicationStatus
{
    PENDING = 1,
    SHORTLISTED = 2,
    ACCEPTED = 3,
    REJECTED = 4,
    WITHDRAWN = 5
}
