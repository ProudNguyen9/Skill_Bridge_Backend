using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.Administration;

public interface IAdminGovernanceService
{
    Task<PagedResponse<AdminUserListItemResponse>> ListUsersAsync(AdminUserListQuery query, CancellationToken cancellationToken);

    Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken);

    Task<AdminPolicyResponse?> GetPolicyAsync(string category, CancellationToken cancellationToken);

    Task<AdminPolicyResponse> UpdatePolicyAsync(Guid actorUserId, string category, string settingsJson, CancellationToken cancellationToken);

    Task<PagedResponse<AdminAuditLogListItemResponse>> ListAuditLogsAsync(AdminAuditLogListQuery query, CancellationToken cancellationToken);
}
