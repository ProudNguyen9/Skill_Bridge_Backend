using DNTU.SkillBridge.Domain.Payments;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Payments;

public sealed class PaymentService(AppDbContext dbContext)
{
    public async Task<FundingOrder?> CreateFundingOrderAsync(Guid companyUserId, Guid projectId, long amount, CancellationToken cancellationToken)
    {
        var companyId = await dbContext.CompanyMembers.AsNoTracking()
            .Where(member => member.UserId == companyUserId)
            .Select(member => (Guid?)member.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!companyId.HasValue || !await dbContext.Projects.AsNoTracking().AnyAsync(project => project.Id == projectId && project.CompanyId == companyId.Value, cancellationToken))
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
        dbContext.FundingOrders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<bool> StartDisbursementProcessingAsync(Guid disbursementId, CancellationToken cancellationToken)
    {
        var disbursement = await dbContext.Disbursements.SingleOrDefaultAsync(item => item.Id == disbursementId, cancellationToken);
        if (disbursement is null) return false;

        try
        {
            disbursement.StartProcessing();
            await dbContext.SaveChangesAsync(cancellationToken);
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
        var disbursement = await dbContext.Disbursements.SingleOrDefaultAsync(item => item.Id == disbursementId, cancellationToken);
        if (disbursement is null)
        {
            return false;
        }

        try
        {
            var changed = disbursement.MarkPaid(idempotencyKey, bankReference, null, DateTimeOffset.UtcNow);
            if (changed)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
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
        var disbursement = await dbContext.Disbursements.SingleOrDefaultAsync(item => item.Id == disbursementId, cancellationToken);
        if (disbursement is null) return false;

        try
        {
            disbursement.MarkFailed(note);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<bool> CancelDisbursementAsync(Guid disbursementId, string? note, CancellationToken cancellationToken)
    {
        var disbursement = await dbContext.Disbursements.SingleOrDefaultAsync(item => item.Id == disbursementId, cancellationToken);
        if (disbursement is null) return false;

        try
        {
            disbursement.Cancel(note);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
