using DNTU.SkillBridge.Domain.Payments;

namespace DNTU.SkillBridge.Application.Payments;

/// <summary>Data operations for funding orders and disbursements.</summary>
public interface IPaymentRepository
{
    Task<Guid?> FindCompanyIdByUserAsync(Guid companyUserId, CancellationToken cancellationToken);

    Task<bool> ProjectBelongsToCompanyAsync(Guid projectId, Guid companyId, CancellationToken cancellationToken);

    void AddFundingOrder(FundingOrder order);

    /// <summary>Reads a disbursement as a tracked entity so mutations are persisted.</summary>
    Task<Disbursement?> FindDisbursementAsync(Guid disbursementId, CancellationToken cancellationToken);
}
