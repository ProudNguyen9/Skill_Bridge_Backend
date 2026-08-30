using System.Security.Cryptography;
using System.Text;
using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Payments;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Api.Payments;

/// <summary>Handles verified SePay callbacks; browser navigation is never payment proof.</summary>
public sealed class SePayService(AppDbContext dbContext, IOptions<SePayOptions> options)
{
    public async Task<SePayCheckoutResponse?> CreateCheckoutAsync(Guid companyUserId, Guid fundingOrderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.FundingOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fundingOrderId, cancellationToken);
        if (order is null || order.Status != FundingOrderStatus.PENDING_PAYMENT || order.ExpiresAt <= DateTimeOffset.UtcNow ||
            !await dbContext.Projects.AnyAsync(project => project.Id == order.ProjectId && project.Company.Members.Any(member => member.UserId == companyUserId), cancellationToken))
        {
            return null;
        }

        var gateway = options.Value.PaymentGateway;
        if (!options.Value.Enabled || !Uri.TryCreate(gateway.CheckoutUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        var payload = $"invoice={Uri.EscapeDataString(order.InvoiceCode)}&amount={order.Amount}&currency={order.Currency}&merchant={Uri.EscapeDataString(gateway.MerchantId!)}";
        var signature = Sign(payload, gateway.SecretKey!);
        return new SePayCheckoutResponse(order.Id, new Uri($"{baseUri}?{payload}&signature={signature}"), order.ExpiresAt);
    }

    public async Task<bool> ApplyIpnAsync(SePayIpnRequest request, CancellationToken cancellationToken)
    {
        var gateway = options.Value.PaymentGateway;
        if (!options.Value.Enabled || !IsValidSignature(request, gateway.IpnSecretKey))
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await dbContext.SePayIpnEvents.AnyAsync(item => item.ProviderEventId == request.EventId || item.ProviderTransactionId == request.TransactionId, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return true;
            }

            var order = await dbContext.FundingOrders.SingleOrDefaultAsync(item => item.InvoiceCode == request.InvoiceCode, cancellationToken);
            if (order is null || order.Status != FundingOrderStatus.PENDING_PAYMENT || order.ExpiresAt <= DateTimeOffset.UtcNow ||
                request.Amount != order.Amount || !string.Equals(request.Currency, order.Currency, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(request.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var eventHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{request.EventId}|{request.InvoiceCode}|{request.TransactionId}|{request.Amount}|{request.Currency}|{request.Status}")));
            var ipnEvent = new SePayIpnEvent(request.EventId, request.InvoiceCode, request.TransactionId, request.Amount, request.Currency, request.Status, eventHash, request.OccurredAt);
            if (!order.MarkPaid("SePay", request.TransactionId, request.OccurredAt))
            {
                return false;
            }

            var funding = await dbContext.ProjectFundings.SingleOrDefaultAsync(item => item.ProjectId == order.ProjectId, cancellationToken);
            if (funding is null)
            {
                funding = new ProjectFunding(order.ProjectId, order.Amount, order.Currency);
                dbContext.ProjectFundings.Add(funding);
            }
            funding.AddFunding(order.Amount);
            dbContext.SePayIpnEvents.Add(ipnEvent);
            dbContext.PaymentTransactions.Add(new PaymentTransaction(order.Id, "SePay", request.TransactionId, request.Amount, request.Currency, PaymentTransactionStatus.SUCCEEDED, request.OccurredAt));
            dbContext.OutboxMessages.Add(new OutboxMessage("payment.funding.succeeded", $"{{\"projectId\":\"{order.ProjectId}\",\"orderId\":\"{order.Id}\"}}"));
            ipnEvent.MarkApplied();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }

    private static bool IsValidSignature(SePayIpnRequest request, string? secret) =>
        !string.IsNullOrWhiteSpace(secret) && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Sign($"{request.EventId}|{request.InvoiceCode}|{request.TransactionId}|{request.Amount}|{request.Currency}|{request.Status}|{request.OccurredAt:O}", secret)),
            Encoding.UTF8.GetBytes(request.Signature));

    private static string Sign(string payload, string secret) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));
}
