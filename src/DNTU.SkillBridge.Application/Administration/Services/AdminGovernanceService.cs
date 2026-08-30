using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Administration;

namespace DNTU.SkillBridge.Application.Administration;

public sealed class AdminGovernanceService(IAdministrationRepository administrationRepository, IUnitOfWork unitOfWork) : IAdminGovernanceService
{
    public Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken) =>
        administrationRepository.GetDashboardAsync(cancellationToken);

    public async Task<AdminPolicyResponse?> GetPolicyAsync(string category, CancellationToken cancellationToken)
    {
        var policy = await administrationRepository.FindPolicyAsync(category, cancellationToken);
        return policy is null ? null : Map(policy);
    }

    public async Task<AdminPolicyResponse> UpdatePolicyAsync(Guid actorUserId, string category, string settingsJson, CancellationToken cancellationToken)
    {
        var policy = await administrationRepository.FindPolicyForUpdateAsync(category, cancellationToken);
        if (policy is null)
        {
            policy = new AdminPolicySetting(category, settingsJson);
            administrationRepository.AddPolicy(policy);
        }
        else
        {
            policy.Update(settingsJson);
        }

        administrationRepository.AddAuditLog(new AuditLog(actorUserId, AuditActions.PolicyUpdated, nameof(AdminPolicySetting), policy.Id.ToString(), null, null, null, null, null, "{}", DateTimeOffset.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(policy);
    }

    private static AdminPolicyResponse Map(AdminPolicySetting policy) => new(policy.Category, policy.SettingsJson, policy.Version, policy.UpdatedAt);
}
