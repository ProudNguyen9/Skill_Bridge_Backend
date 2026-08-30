using System.Security.Cryptography;
using System.Text;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Payments;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Application.SePay;

public interface ISePayService
{
    Task<SePayCheckoutResponse?> CreateCheckoutAsync(Guid companyUserId, Guid fundingOrderId, CancellationToken cancellationToken);

    Task<bool> ApplyIpnAsync(SePayIpnRequest request, CancellationToken cancellationToken);
}

/// <summary>Handles verified SePay callbacks; browser navigation is never payment proof.</summary>
public sealed class SePayService(ISePayRepository sePayRepository, IOutboxEnqueuer outboxEnqueuer, IUnitOfWork unitOfWork, IOptions<SePayOptions> options) : ISePayService
{
    public async Task<SePayCheckoutResponse?> CreateCheckoutAsync(Guid companyUserId, Guid fundingOrderId, CancellationToken cancellationToken)
    {
        var order = await sePayRepository.FindFundingOrderAsync(fundingOrderId, cancellationToken);
        if (order is null || order.Status != FundingOrderStatus.PENDING_PAYMENT || order.ExpiresAt <= DateTimeOffset.UtcNow ||
            !await sePayRepository.ProjectBelongsToUserAsync(order.ProjectId, companyUserId, cancellationToken))
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

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await sePayRepository.HasIpnEventAsync(request.EventId, request.TransactionId, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return true;
            }

            var order = await sePayRepository.FindFundingOrderByInvoiceAsync(request.InvoiceCode, cancellationToken);
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

            var funding = await sePayRepository.FindProjectFundingAsync(order.ProjectId, cancellationToken);
            if (funding is null)
            {
                funding = new ProjectFunding(order.ProjectId, order.Amount, order.Currency);
                sePayRepository.AddProjectFunding(funding);
            }
            funding.AddFunding(order.Amount);
            sePayRepository.AddSePayIpnEvent(ipnEvent);
            sePayRepository.AddPaymentTransaction(new PaymentTransaction(order.Id, "SePay", request.TransactionId, request.Amount, request.Currency, PaymentTransactionStatus.SUCCEEDED, request.OccurredAt));
            outboxEnqueuer.Enqueue("payment.funding.succeeded", $"{{\"projectId\":\"{order.ProjectId}\",\"orderId\":\"{order.Id}\"}}");
            ipnEvent.MarkApplied();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (PersistenceException)
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
