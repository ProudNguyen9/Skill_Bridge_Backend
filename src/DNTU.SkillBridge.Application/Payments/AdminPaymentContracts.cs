using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Payments;

namespace DNTU.SkillBridge.Application.Payments;

public sealed class AdminFundingOrderListQuery : PageQuery
{
    public string? Status { get; init; }
}

public sealed record AdminFundingOrderListItemResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectTitle,
    string? CompanyName,
    string InvoiceCode,
    long Amount,
    string Currency,
    FundingOrderStatus Status,
    string? Provider,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt);

public sealed class AdminDisbursementListQuery : PageQuery
{
    public string? Status { get; init; }
}

public sealed record AdminDisbursementListItemResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectTitle,
    Guid StudentId,
    string StudentName,
    long Amount,
    string Currency,
    DisbursementStatus Status,
    string? BankReference,
    string? Note,
    DateTimeOffset? PaidAt);
