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
}
