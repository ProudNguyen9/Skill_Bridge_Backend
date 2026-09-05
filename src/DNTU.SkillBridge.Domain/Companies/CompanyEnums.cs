namespace DNTU.SkillBridge.Domain.Companies;

public enum CompanyVerificationStatus
{
    PENDING = 1,
    VERIFIED = 2,
    REJECTED = 3,
    SUSPENDED = 4
}

public enum CompanyMemberRole
{
    OWNER = 1,
    MANAGER = 2,
    MEMBER = 3
}

public enum CompanyInvitationStatus
{
    PENDING = 1,
    ACCEPTED = 2,
    REVOKED = 3
}
