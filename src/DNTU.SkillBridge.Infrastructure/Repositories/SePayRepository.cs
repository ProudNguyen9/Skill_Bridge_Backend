using DNTU.SkillBridge.Application.SePay;
using DNTU.SkillBridge.Domain.Payments;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the SePay data operations.</summary>
public sealed class SePayRepository(AppDbContext dbContext) : ISePayRepository
{
    public Task<FundingOrder?> FindFundingOrderAsync(Guid fundingOrderId, CancellationToken cancellationToken) =>
        dbContext.FundingOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fundingOrderId, cancellationToken);

    public Task<bool> ProjectBelongsToUserAsync(Guid projectId, Guid companyUserId, CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member => member.UserId == companyUserId), cancellationToken);

    public Task<bool> HasIpnEventAsync(string eventId, string transactionId, CancellationToken cancellationToken) =>
        dbContext.SePayIpnEvents.AnyAsync(item => item.ProviderEventId == eventId || item.ProviderTransactionId == transactionId, cancellationToken);

    public Task<FundingOrder?> FindFundingOrderByInvoiceAsync(string invoiceCode, CancellationToken cancellationToken) =>
        dbContext.FundingOrders.AsTracking().SingleOrDefaultAsync(item => item.InvoiceCode == invoiceCode, cancellationToken);

    public Task<ProjectFunding?> FindProjectFundingAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectFundings.AsTracking().SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);

    public void AddProjectFunding(ProjectFunding funding) => dbContext.ProjectFundings.Add(funding);

    public void AddSePayIpnEvent(SePayIpnEvent ipnEvent) => dbContext.SePayIpnEvents.Add(ipnEvent);

    public void AddPaymentTransaction(PaymentTransaction transaction) => dbContext.PaymentTransactions.Add(transaction);

    public async Task<(string ProjectTitle, IReadOnlyCollection<Guid> CompanyUserIds)?> GetProjectFundingNotificationScopeAsync(
        Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Title, p.CompanyId })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null) return null;

        var companyUserIds = await dbContext.CompanyMembers.AsNoTracking()
            .Where(m => m.CompanyId == project.CompanyId)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return (project.Title, companyUserIds);
    }
}
