using DNTU.SkillBridge.Application.Administration;
using DNTU.SkillBridge.Domain.Administration;

namespace DNTU.SkillBridge.Application.Administration;

/// <summary>Data operations for the admin governance dashboard and policy settings.</summary>
public interface IAdministrationRepository
{
    Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken);

    /// <summary>Reads a policy setting without tracking it.</summary>
    Task<AdminPolicySetting?> FindPolicyAsync(string category, CancellationToken cancellationToken);

    /// <summary>Reads a policy setting as a tracked entity so mutations are persisted.</summary>
    Task<AdminPolicySetting?> FindPolicyForUpdateAsync(string category, CancellationToken cancellationToken);

    void AddPolicy(AdminPolicySetting policy);

    void AddAuditLog(AuditLog auditLog);
}
