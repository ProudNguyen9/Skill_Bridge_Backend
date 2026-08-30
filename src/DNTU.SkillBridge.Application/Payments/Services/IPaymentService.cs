using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Payments;

namespace DNTU.SkillBridge.Application.Payments;

public interface IPaymentService
{
    Task<FundingOrder?> CreateFundingOrderAsync(Guid companyUserId, Guid projectId, long amount, CancellationToken cancellationToken);

    Task<bool> StartDisbursementProcessingAsync(Guid disbursementId, CancellationToken cancellationToken);

    Task<bool> MarkDisbursementPaidAsync(Guid disbursementId, string idempotencyKey, string bankReference, CancellationToken cancellationToken);

    Task<bool> MarkDisbursementFailedAsync(Guid disbursementId, string? note, CancellationToken cancellationToken);

    Task<bool> CancelDisbursementAsync(Guid disbursementId, string? note, CancellationToken cancellationToken);
}
