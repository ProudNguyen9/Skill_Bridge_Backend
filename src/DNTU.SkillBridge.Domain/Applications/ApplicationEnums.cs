namespace DNTU.SkillBridge.Domain.Applications;

/// <summary>
/// Lifecycle state of a student application. Member names intentionally match the
/// uppercase wire values.
/// </summary>
public enum ApplicationStatus
{
    PENDING = 1,
    SHORTLISTED = 2,
    ACCEPTED = 3,
    REJECTED = 4,
    WITHDRAWN = 5,
    WAITING_MEMBER_CONSENT = 6,
    ADJUSTMENT_REQUESTED = 7,
    WAITING_LEADER_APPROVAL = 8
}

public enum ApplicationType
{
    INDIVIDUAL = 1,
    TEAM = 2
}

public enum MemberConsentStatus
{
    PENDING = 1,
    APPROVED = 2,
    DECLINED = 3
}
