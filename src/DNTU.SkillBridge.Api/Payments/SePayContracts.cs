using System.ComponentModel.DataAnnotations;

namespace DNTU.SkillBridge.Api.Payments;

public sealed record SePayCheckoutResponse(Guid FundingOrderId, Uri CheckoutUrl, DateTimeOffset ExpiresAt);

public sealed class SePayIpnRequest
{
    [Required, StringLength(128)] public string EventId { get; init; } = string.Empty;
    [Required, StringLength(64)] public string InvoiceCode { get; init; } = string.Empty;
    [Required, StringLength(128)] public string TransactionId { get; init; } = string.Empty;
    [Range(1, long.MaxValue)] public long Amount { get; init; }
    [Required, StringLength(3, MinimumLength = 3)] public string Currency { get; init; } = "VND";
    [Required, StringLength(40)] public string Status { get; init; } = string.Empty;
    [Required, StringLength(128)] public string Signature { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
}
