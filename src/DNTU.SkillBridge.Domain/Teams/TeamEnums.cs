namespace DNTU.SkillBridge.Domain.Teams;

public enum TeamMemberRole
{
    LEADER = 1,
    MEMBER = 2
}

public enum TeamInvitationStatus
{
    PENDING = 1,
    ACCEPTED = 2,
    REJECTED = 3,
    REVOKED = 4,
    EXPIRED = 5
}

public enum TeamType
{
    PROJECT = 1,
    FIXED = 2
}

public enum TeamRecommendationStatus
{
    PENDING = 1,
    ACCEPTED = 2,
    DECLINED = 3
}
