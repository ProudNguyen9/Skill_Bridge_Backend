namespace DNTU.SkillBridge.Application.Projects;

public enum ProjectCreateOutcome
{
    Created,
    CompanyNotFound,
    InvalidCatalog,
    InvalidTeamSize,
    Conflict
}

public enum ProjectUpdateOutcome
{
    Updated,
    NotFound,
    NotDraft,
    InvalidCatalog,
    InvalidTeamSize,
    Conflict
}

public enum ProjectDeleteOutcome
{
    Deleted,
    NotFound,
    NotDraft
}

public enum ProjectSubmitOutcome
{
    Submitted,
    NotFound,
    CompanyNotVerified,
    Incomplete,
    InvalidDeadline,
    NotSubmittable
}

public enum ProjectCancelOutcome
{
    Cancelled,
    NotFound,
    InvalidTransition
}

public enum ProjectReopenOutcome
{
    Reopened,
    NotFound,
    InvalidTransition
}

public enum AdminDecisionOutcome
{
    Applied,
    NotFound,
    InvalidTransition
}
