using DNTU.SkillBridge.Domain.Common;

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

public sealed class ProjectFunding : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public long RequiredAmount { get; private set; }
    public long FundedAmount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public ProjectFundingStatus Status { get; private set; } = ProjectFundingStatus.NOT_FUNDED;

    private ProjectFunding() { }

    public ProjectFunding(Guid projectId, long requiredAmount, string currency)
    {
        if (requiredAmount < 0) throw new ArgumentOutOfRangeException(nameof(requiredAmount));
        ProjectId = projectId;
        RequiredAmount = requiredAmount;
        Currency = NormalizeCurrency(currency);
        Status = requiredAmount == 0 ? ProjectFundingStatus.NOT_REQUIRED : ProjectFundingStatus.NOT_FUNDED;
    }

    public void AddFunding(long amount)
    {
        if (Status == ProjectFundingStatus.CANCELLED) throw new InvalidOperationException("Cancelled project funding cannot receive funds.");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        FundedAmount += amount;
        Status = RequiredAmount == 0
            ? ProjectFundingStatus.NOT_REQUIRED
            : FundedAmount >= RequiredAmount
                ? ProjectFundingStatus.FUNDED
                : ProjectFundingStatus.PARTIALLY_FUNDED;
    }

    public void Cancel() => Status = ProjectFundingStatus.CANCELLED;

    private static string NormalizeCurrency(string currency) =>
        string.IsNullOrWhiteSpace(currency) ? throw new ArgumentException("A currency is required.", nameof(currency)) : currency.Trim().ToUpperInvariant();
}

/// <summary>Company funding order; payment gateway callbacks alone may transition it to PAID.</summary>
public sealed class FundingOrder : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public string InvoiceCode { get; private set; } = string.Empty;
    public long Amount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public FundingOrderStatus Status { get; private set; } = FundingOrderStatus.CREATED;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public string? Provider { get; private set; }
    public string? ProviderTransactionId { get; private set; }

    private FundingOrder() { }

    public FundingOrder(Guid projectId, string invoiceCode, long amount, string currency, DateTimeOffset expiresAt)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        ProjectId = projectId;
        InvoiceCode = string.IsNullOrWhiteSpace(invoiceCode) ? throw new ArgumentException("An invoice code is required.", nameof(invoiceCode)) : invoiceCode.Trim();
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? throw new ArgumentException("A currency is required.", nameof(currency)) : currency.Trim().ToUpperInvariant();
        ExpiresAt = expiresAt;
    }

    public void MarkPending() => Transition(FundingOrderStatus.CREATED, FundingOrderStatus.PENDING_PAYMENT);
    public bool MarkPaid(string provider, string providerTransactionId, DateTimeOffset paidAt)
    {
        if (Status == FundingOrderStatus.PAID)
        {
            return Provider == provider && ProviderTransactionId == providerTransactionId;
        }

        if (Status != FundingOrderStatus.PENDING_PAYMENT) throw new InvalidOperationException($"Cannot transition funding order from {Status} to PAID.");
        Provider = Required(provider, nameof(provider), 40);
        ProviderTransactionId = Required(providerTransactionId, nameof(providerTransactionId), 128);
        PaidAt = paidAt;
        Status = FundingOrderStatus.PAID;
        return true;
    }

    public void Cancel() => Transition(FundingOrderStatus.CREATED, FundingOrderStatus.CANCELLED);
    public void Expire(DateTimeOffset now)
    {
        if (Status == FundingOrderStatus.PENDING_PAYMENT && ExpiresAt <= now)
        {
            Status = FundingOrderStatus.EXPIRED;
        }
    }

    private void Transition(FundingOrderStatus from, FundingOrderStatus to)
    {
        if (Status != from) throw new InvalidOperationException($"Cannot transition funding order from {Status} to {to}.");
        Status = to;
    }

    private static string Required(string value, string name, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength ? value.Trim() : throw new ArgumentException($"{name} is required.", name);
}

public sealed class PaymentTransaction : AuditableEntity
{
    public Guid FundingOrderId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ProviderTransactionId { get; private set; } = string.Empty;
    public long Amount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public PaymentTransactionStatus Status { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private PaymentTransaction() { }

    public PaymentTransaction(Guid fundingOrderId, string provider, string providerTransactionId, long amount, string currency, PaymentTransactionStatus status, DateTimeOffset occurredAt)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        FundingOrderId = fundingOrderId;
        Provider = Required(provider, nameof(provider), 40);
        ProviderTransactionId = Required(providerTransactionId, nameof(providerTransactionId), 128);
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? throw new ArgumentException("A currency is required.", nameof(currency)) : currency.Trim().ToUpperInvariant();
        Status = status;
        OccurredAt = occurredAt;
    }

    private static string Required(string value, string name, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength ? value.Trim() : throw new ArgumentException($"{name} is required.", name);
}

public sealed class MilestoneAllowance : AuditableEntity
{
    public Guid MilestoneId { get; private set; }
    public long Amount { get; private set; }
    public string Currency { get; private set; } = "VND";

    private MilestoneAllowance() { }

    public MilestoneAllowance(Guid milestoneId, long amount, string currency)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        MilestoneId = milestoneId;
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? throw new ArgumentException("A currency is required.", nameof(currency)) : currency.Trim().ToUpperInvariant();
    }

    public void Update(long amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Amount = amount;
    }
}

public sealed class AllowanceAllocation : AuditableEntity
{
    public Guid MilestoneAllowanceId { get; private set; }
    public Guid StudentId { get; private set; }
    public long Amount { get; private set; }

    private AllowanceAllocation() { }

    public AllowanceAllocation(Guid milestoneAllowanceId, Guid studentId, long amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        MilestoneAllowanceId = milestoneAllowanceId;
        StudentId = studentId;
        Amount = amount;
    }
}

/// <summary>Student payment record whose paid transition is protected by an idempotency key.</summary>
public sealed class Disbursement : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public Guid StudentId { get; private set; }
    public long Amount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public DisbursementStatus Status { get; private set; } = DisbursementStatus.ELIGIBLE;
    public string? IdempotencyKey { get; private set; }
    public string? BankReference { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }

    private Disbursement() { }

    public Disbursement(Guid projectId, Guid studentId, long amount, string currency)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        ProjectId = projectId;
        StudentId = studentId;
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? throw new ArgumentException("A currency is required.", nameof(currency)) : currency.Trim().ToUpperInvariant();
    }

    public void StartProcessing()
    {
        if (Status != DisbursementStatus.ELIGIBLE) throw new InvalidOperationException("Only eligible disbursements can start processing.");
        Status = DisbursementStatus.PROCESSING;
    }

    public bool MarkPaid(string idempotencyKey, string bankReference, string? note, DateTimeOffset paidAt)
    {
        if (Status == DisbursementStatus.PAID && IdempotencyKey == idempotencyKey) return false;
        if (Status is not (DisbursementStatus.ELIGIBLE or DisbursementStatus.PROCESSING) || string.IsNullOrWhiteSpace(idempotencyKey) || string.IsNullOrWhiteSpace(bankReference)) throw new InvalidOperationException("The disbursement is not eligible to be paid.");
        Status = DisbursementStatus.PAID;
        IdempotencyKey = idempotencyKey.Trim();
        BankReference = bankReference.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        PaidAt = paidAt;
        return true;
    }

    public void MarkFailed(string? note)
    {
        if (Status is not (DisbursementStatus.ELIGIBLE or DisbursementStatus.PROCESSING)) throw new InvalidOperationException("Only eligible or processing disbursements can fail.");
        Status = DisbursementStatus.FAILED;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void Cancel(string? note)
    {
        if (Status == DisbursementStatus.PAID) throw new InvalidOperationException("Paid disbursements cannot be cancelled.");
        Status = DisbursementStatus.CANCELLED;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

public sealed class StudentPayoutAccount : AuditableEntity
{
    public Guid StudentId { get; private set; }
    public string BankCode { get; private set; } = string.Empty;
    public string AccountHolderName { get; private set; } = string.Empty;
    public string AccountNumberCiphertext { get; private set; } = string.Empty;
    public string AccountNumberLast4 { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }

    private StudentPayoutAccount() { }

    public StudentPayoutAccount(Guid studentId, string bankCode, string accountHolderName, string accountNumberCiphertext, string accountNumberLast4, bool isDefault)
    {
        StudentId = studentId;
        BankCode = Required(bankCode, nameof(bankCode), 30).ToUpperInvariant();
        AccountHolderName = Required(accountHolderName, nameof(accountHolderName), 200);
        AccountNumberCiphertext = Required(accountNumberCiphertext, nameof(accountNumberCiphertext), 2000);
        AccountNumberLast4 = Required(accountNumberLast4, nameof(accountNumberLast4), 4);
        IsDefault = isDefault;
    }

    public string MaskedAccountNumber => $"****{AccountNumberLast4}";

    private static string Required(string value, string name, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength ? value.Trim() : throw new ArgumentException($"{name} is required.", name);
}

public sealed class IdempotencyRecord : AuditableEntity
{
    public Guid? UserId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public string ResponseJson { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }

    private IdempotencyRecord() { }

    public IdempotencyRecord(Guid? userId, string key, string endpoint, string requestHash, string responseJson, DateTimeOffset expiresAt)
    {
        UserId = userId;
        Key = Required(key, nameof(key), 128);
        Endpoint = Required(endpoint, nameof(endpoint), 200);
        RequestHash = Required(requestHash, nameof(requestHash), 128);
        ResponseJson = Required(responseJson, nameof(responseJson), 10000);
        ExpiresAt = expiresAt;
    }

    private static string Required(string value, string name, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength ? value.Trim() : throw new ArgumentException($"{name} is required.", name);
}

public sealed class SePayIpnEvent : AuditableEntity
{
    public string ProviderEventId { get; private set; } = string.Empty;
    public string InvoiceCode { get; private set; } = string.Empty;
    public string ProviderTransactionId { get; private set; } = string.Empty;
    public long Amount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public string Status { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private set; }
    public bool Applied { get; private set; }
    public string? FailureReason { get; private set; }

    private SePayIpnEvent() { }

    public SePayIpnEvent(string providerEventId, string invoiceCode, string providerTransactionId, long amount, string currency, string status, string payloadHash, DateTimeOffset receivedAt)
    {
        ProviderEventId = Required(providerEventId, nameof(providerEventId), 128);
        InvoiceCode = Required(invoiceCode, nameof(invoiceCode), 64);
        ProviderTransactionId = Required(providerTransactionId, nameof(providerTransactionId), 128);
        Amount = amount;
        Currency = Required(currency, nameof(currency), 3).ToUpperInvariant();
        Status = Required(status, nameof(status), 40).ToUpperInvariant();
        PayloadHash = Required(payloadHash, nameof(payloadHash), 128);
        ReceivedAt = receivedAt;
    }

    public void MarkApplied() => Applied = true;
    public void MarkRejected(string reason) => FailureReason = Required(reason, nameof(reason), 300);

    private static string Required(string value, string name, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength ? value.Trim() : throw new ArgumentException($"{name} is required.", name);
}

public sealed class ReconciliationRun : AuditableEntity
{
    public ReconciliationRunStatus Status { get; private set; } = ReconciliationRunStatus.RUNNING;
    public int CheckedCount { get; private set; }
    public int RepairedCount { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    private ReconciliationRun() { }

    public ReconciliationRun(DateTimeOffset startedAt) => StartedAt = startedAt;

    public void Complete(int checkedCount, int repairedCount, DateTimeOffset finishedAt)
    {
        CheckedCount = checkedCount;
        RepairedCount = repairedCount;
        FinishedAt = finishedAt;
        Status = ReconciliationRunStatus.COMPLETED;
    }

    public void Fail(string safeError, DateTimeOffset finishedAt)
    {
        Error = string.IsNullOrWhiteSpace(safeError) ? "Reconciliation failed." : safeError.Trim()[..Math.Min(300, safeError.Trim().Length)];
        FinishedAt = finishedAt;
        Status = ReconciliationRunStatus.FAILED;
    }
}
