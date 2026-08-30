namespace DNTU.SkillBridge.Application.Applications;

public enum ApplicationApplyOutcome
{
    Applied,
    StudentNotFound,
    ProjectNotFound,
    ProjectNotAccepting,
    AlreadyApplied
}

public enum ApplicationWithdrawOutcome
{
    Withdrawn,
    NotFound,
    NotWithdrawable
}
