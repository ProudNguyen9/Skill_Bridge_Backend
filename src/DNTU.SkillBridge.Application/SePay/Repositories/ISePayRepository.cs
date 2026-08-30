using DNTU.SkillBridge.Domain.Payments;

namespace DNTU.SkillBridge.Application.SePay;

/// <summary>Data operations for the SePay checkout and IPN flows.</summary>
public interface ISePayRepository
{
    Task<FundingOrder?> FindFundingOrderAsync(Guid fundingOrderId, CancellationToken cancellationToken);

    Task<bool> ProjectBelongsToUserAsync(Guid projectId, Guid companyUserId, CancellationToken cancellationToken);

    Task<bool> HasIpnEventAsync(string eventId, string transactionId, CancellationToken cancellationToken);

    /// <summary>Reads a funding order by invoice code as a tracked entity so mutations are persisted.</summary>
    Task<FundingOrder?> FindFundingOrderByInvoiceAsync(string invoiceCode, CancellationToken cancellationToken);

    /// <summary>Reads a project funding as a tracked entity so mutations are persisted.</summary>
    Task<ProjectFunding?> FindProjectFundingAsync(Guid projectId, CancellationToken cancellationToken);

    void AddProjectFunding(ProjectFunding funding);

    void AddSePayIpnEvent(SePayIpnEvent ipnEvent);

    void AddPaymentTransaction(PaymentTransaction transaction);
}
