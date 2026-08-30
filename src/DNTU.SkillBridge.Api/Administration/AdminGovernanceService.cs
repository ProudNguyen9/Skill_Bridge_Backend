using DNTU.SkillBridge.Domain.Administration;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Administration;

public sealed record AdminDashboardResponse(int Companies, int Students, int Lecturers, int Projects, int PendingFundingOrders, int EligibleDisbursements);
public sealed record AdminPolicyResponse(string Category, string SettingsJson, int Version, DateTimeOffset? UpdatedAt);

public sealed class AdminGovernanceService(AppDbContext dbContext)
{
    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken) => new(
        await dbContext.Companies.CountAsync(cancellationToken),
        await dbContext.StudentProfiles.CountAsync(cancellationToken),
        await dbContext.LecturerProfiles.CountAsync(cancellationToken),
        await dbContext.Projects.CountAsync(cancellationToken),
        await dbContext.FundingOrders.CountAsync(item => item.Status == Domain.Payments.FundingOrderStatus.PENDING_PAYMENT, cancellationToken),
        await dbContext.Disbursements.CountAsync(item => item.Status == Domain.Payments.DisbursementStatus.ELIGIBLE, cancellationToken));

    public async Task<AdminPolicyResponse?> GetPolicyAsync(string category, CancellationToken cancellationToken)
    {
        var policy = await dbContext.AdminPolicySettings.AsNoTracking().SingleOrDefaultAsync(item => item.Category == category, cancellationToken);
        return policy is null ? null : Map(policy);
    }

    public async Task<AdminPolicyResponse> UpdatePolicyAsync(Guid actorUserId, string category, string settingsJson, CancellationToken cancellationToken)
    {
        var policy = await dbContext.AdminPolicySettings.SingleOrDefaultAsync(item => item.Category == category, cancellationToken);
        if (policy is null)
        {
            policy = new AdminPolicySetting(category, settingsJson);
            dbContext.AdminPolicySettings.Add(policy);
        }
        else
        {
            policy.Update(settingsJson);
        }

        dbContext.AuditLogs.Add(new AuditLog(actorUserId, AuditActions.PolicyUpdated, nameof(AdminPolicySetting), policy.Id.ToString(), null, null, null, null, null, "{}", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(policy);
    }

    private static AdminPolicyResponse Map(AdminPolicySetting policy) => new(policy.Category, policy.SettingsJson, policy.Version, policy.UpdatedAt);
}
