using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Payments;
using DNTU.SkillBridge.Domain.Payments;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the payment data operations.</summary>
public sealed class PaymentRepository(AppDbContext dbContext) : IPaymentRepository
{
    public Task<Guid?> FindCompanyIdByUserAsync(Guid companyUserId, CancellationToken cancellationToken) =>
        dbContext.CompanyMembers.AsNoTracking()
            .Where(member => member.UserId == companyUserId)
            .Select(member => (Guid?)member.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> ProjectBelongsToCompanyAsync(Guid projectId, Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Projects.AsNoTracking().AnyAsync(project => project.Id == projectId && project.CompanyId == companyId, cancellationToken);

    public void AddFundingOrder(FundingOrder order) => dbContext.FundingOrders.Add(order);

    public Task<Disbursement?> FindDisbursementAsync(Guid disbursementId, CancellationToken cancellationToken) =>
        dbContext.Disbursements.AsTracking().SingleOrDefaultAsync(item => item.Id == disbursementId, cancellationToken);

    public async Task<PagedResponse<AdminFundingOrderListItemResponse>> ListFundingOrdersAdminAsync(
        AdminFundingOrderListQuery query, CancellationToken cancellationToken)
    {
        var orders = from fundingOrder in dbContext.FundingOrders.AsNoTracking()
                     join project in dbContext.Projects.AsNoTracking() on fundingOrder.ProjectId equals project.Id
                     join company in dbContext.Companies.AsNoTracking() on project.CompanyId equals company.Id into companyJoin
                     from company in companyJoin.DefaultIfEmpty()
                     select new { fundingOrder, project, company };

        if (Enum.TryParse<FundingOrderStatus>(query.Status, ignoreCase: true, out var status))
            orders = orders.Where(item => item.fundingOrder.Status == status);

        var totalItems = await orders.CountAsync(cancellationToken);
        var metadata = PageMetadata.Create(query.Page, query.PageSize, totalItems);
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= totalItems)
            return new PagedResponse<AdminFundingOrderListItemResponse>([], metadata);

        var items = await orders
            .OrderByDescending(item => item.fundingOrder.CreatedAt)
            .ThenBy(item => item.fundingOrder.Id)
            .Skip((int)offset)
            .Take(query.PageSize)
            .Select(item => new AdminFundingOrderListItemResponse(
                item.fundingOrder.Id, item.fundingOrder.ProjectId, item.project.Title,
                item.company != null ? item.company.Name : null,
                item.fundingOrder.InvoiceCode, item.fundingOrder.Amount, item.fundingOrder.Currency,
                item.fundingOrder.Status, item.fundingOrder.Provider,
                item.fundingOrder.CreatedAt, item.fundingOrder.PaidAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AdminFundingOrderListItemResponse>(items, metadata);
    }

    public async Task<PagedResponse<AdminDisbursementListItemResponse>> ListDisbursementsAdminAsync(
        AdminDisbursementListQuery query, CancellationToken cancellationToken)
    {
        var disbursements = from disbursement in dbContext.Disbursements.AsNoTracking()
                            join project in dbContext.Projects.AsNoTracking() on disbursement.ProjectId equals project.Id
                            join student in dbContext.StudentProfiles.AsNoTracking() on disbursement.StudentId equals student.Id
                            join studentUser in dbContext.Users.AsNoTracking() on student.UserId equals studentUser.Id
                            select new { disbursement, project, studentUser };

        if (Enum.TryParse<DisbursementStatus>(query.Status, ignoreCase: true, out var status))
            disbursements = disbursements.Where(item => item.disbursement.Status == status);

        var totalItems = await disbursements.CountAsync(cancellationToken);
        var metadata = PageMetadata.Create(query.Page, query.PageSize, totalItems);
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= totalItems)
            return new PagedResponse<AdminDisbursementListItemResponse>([], metadata);

        var items = await disbursements
            .OrderByDescending(item => item.disbursement.CreatedAt)
            .ThenBy(item => item.disbursement.Id)
            .Skip((int)offset)
            .Take(query.PageSize)
            .Select(item => new AdminDisbursementListItemResponse(
                item.disbursement.Id, item.disbursement.ProjectId, item.project.Title,
                item.disbursement.StudentId, item.studentUser.DisplayName,
                item.disbursement.Amount, item.disbursement.Currency, item.disbursement.Status,
                item.disbursement.BankReference, item.disbursement.Note, item.disbursement.PaidAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AdminDisbursementListItemResponse>(items, metadata);
    }
}
