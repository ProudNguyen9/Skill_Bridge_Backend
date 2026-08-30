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

public sealed class PaymentService(IPaymentRepository paymentRepository, IUnitOfWork unitOfWork) : IPaymentService
{
    public async Task<FundingOrder?> CreateFundingOrderAsync(Guid companyUserId, Guid projectId, long amount, CancellationToken cancellationToken)
    {
        var companyId = await paymentRepository.FindCompanyIdByUserAsync(companyUserId, cancellationToken);
        if (!companyId.HasValue || !await paymentRepository.ProjectBelongsToCompanyAsync(projectId, companyId.Value, cancellationToken))
        {
            return null;
        }

        var order = new FundingOrder(
            projectId,
            $"FUND-{Guid.CreateVersion7():N}"[..20],
            amount,
            "VND",
            DateTimeOffset.UtcNow.AddMinutes(15));
        order.MarkPending();
        paymentRepository.AddFundingOrder(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<bool> StartDisbursementProcessingAsync(Guid disbursementId, CancellationToken cancellationToken)
    {
        var disbursement = await paymentRepository.FindDisbursementAsync(disbursementId, cancellationToken);
        if (disbursement is null) return false;

        try
        {
            disbursement.StartProcessing();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<bool> MarkDisbursementPaidAsync(
        Guid disbursementId,
        string idempotencyKey,
        string bankReference,
        CancellationToken cancellationToken)
    {
        var disbursement = await paymentRepository.FindDisbursementAsync(disbursementId, cancellationToken);
        if (disbursement is null)
        {
            return false;
        }

        try
        {
            var changed = disbursement.MarkPaid(idempotencyKey, bankReference, null, DateTimeOffset.UtcNow);
            if (changed)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<bool> MarkDisbursementFailedAsync(Guid disbursementId, string? note, CancellationToken cancellationToken)
    {
        var disbursement = await paymentRepository.FindDisbursementAsync(disbursementId, cancellationToken);
        if (disbursement is null) return false;

        try
        {
            disbursement.MarkFailed(note);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<bool> CancelDisbursementAsync(Guid disbursementId, string? note, CancellationToken cancellationToken)
    {
        var disbursement = await paymentRepository.FindDisbursementAsync(disbursementId, cancellationToken);
        if (disbursement is null) return false;

        try
        {
            disbursement.Cancel(note);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
