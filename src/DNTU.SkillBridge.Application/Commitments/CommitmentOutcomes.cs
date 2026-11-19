namespace DNTU.SkillBridge.Application.Commitments;

public enum WithdrawalOutcome
{
    Created,
    NotFound,
    Conflict,
    Forbidden,
    Updated
}

public enum CommitmentSignOutcome
{
    Updated,
    NotFound,
    Forbidden,
    Conflict
}
