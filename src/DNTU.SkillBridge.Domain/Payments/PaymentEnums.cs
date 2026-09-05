namespace DNTU.SkillBridge.Domain.Payments;

public enum ProjectFundingStatus
{
    NOT_REQUIRED = 1,
    NOT_FUNDED = 2,
    PARTIALLY_FUNDED = 3,
    FUNDED = 4,
    CANCELLED = 5
}

public enum FundingOrderStatus
{
    CREATED = 1,
    PENDING_PAYMENT = 2,
    PAID = 3,
    FAILED = 4,
    CANCELLED = 5,
    EXPIRED = 6
}

public enum PaymentTransactionStatus
{
    SUCCEEDED = 1,
    FAILED = 2
}

public enum DisbursementStatus
{
    NOT_ELIGIBLE = 1,
    ELIGIBLE = 2,
    PROCESSING = 3,
    PAID = 4,
    FAILED = 5,
    CANCELLED = 6
}

public enum ReconciliationRunStatus
{
    RUNNING = 1,
    COMPLETED = 2,
    FAILED = 3
}
