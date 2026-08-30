using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Administration;

namespace DNTU.SkillBridge.Application.Administration;

public interface IAdminGovernanceService
{
    Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken);

    Task<AdminPolicyResponse?> GetPolicyAsync(string category, CancellationToken cancellationToken);

    Task<AdminPolicyResponse> UpdatePolicyAsync(Guid actorUserId, string category, string settingsJson, CancellationToken cancellationToken);
}
