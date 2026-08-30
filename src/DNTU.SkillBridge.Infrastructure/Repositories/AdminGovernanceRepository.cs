using DNTU.SkillBridge.Application.Administration;
using DNTU.SkillBridge.Domain.Administration;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the admin governance data operations.</summary>
public sealed class AdminGovernanceRepository(AppDbContext dbContext) : IAdministrationRepository
{
    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken) => new(
        await dbContext.Companies.CountAsync(cancellationToken),
        await dbContext.StudentProfiles.CountAsync(cancellationToken),
        await dbContext.LecturerProfiles.CountAsync(cancellationToken),
        await dbContext.Projects.CountAsync(cancellationToken),
        await dbContext.FundingOrders.CountAsync(item => item.Status == Domain.Payments.FundingOrderStatus.PENDING_PAYMENT, cancellationToken),
        await dbContext.Disbursements.CountAsync(item => item.Status == Domain.Payments.DisbursementStatus.ELIGIBLE, cancellationToken));

    public Task<AdminPolicySetting?> FindPolicyAsync(string category, CancellationToken cancellationToken) =>
        dbContext.AdminPolicySettings.AsNoTracking().SingleOrDefaultAsync(item => item.Category == category, cancellationToken);

    public Task<AdminPolicySetting?> FindPolicyForUpdateAsync(string category, CancellationToken cancellationToken) =>
        dbContext.AdminPolicySettings.AsTracking().SingleOrDefaultAsync(item => item.Category == category, cancellationToken);

    public void AddPolicy(AdminPolicySetting policy) => dbContext.AdminPolicySettings.Add(policy);

    public void AddAuditLog(AuditLog auditLog) => dbContext.AuditLogs.Add(auditLog);
}
