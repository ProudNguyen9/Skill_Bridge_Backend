namespace DNTU.SkillBridge.Application.Companies;

public enum CompanyUpdateOutcome
{
    Created,
    Updated,
    NameRequired,
    Conflict
}

public enum CompanyRemoveMemberOutcome
{
    Removed,
    NotMember,
    LastOwner,
    Forbidden
}
