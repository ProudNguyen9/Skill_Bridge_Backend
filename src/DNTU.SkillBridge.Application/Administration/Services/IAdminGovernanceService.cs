using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.Administration;

public interface IAdminGovernanceService
{
    Task<PagedResponse<AdminUserListItemResponse>> ListUsersAsync(AdminUserListQuery query, CancellationToken cancellationToken);

    /// <summary>Đọc một user theo id để phản hồi kết quả thay đổi trạng thái/vai trò.</summary>
    Task<AdminUserListItemResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken);

    Task<AdminPolicyResponse?> GetPolicyAsync(string category, CancellationToken cancellationToken);

    Task<AdminPolicyResponse> UpdatePolicyAsync(Guid actorUserId, string category, string settingsJson, CancellationToken cancellationToken);

    Task<PagedResponse<AdminAuditLogListItemResponse>> ListAuditLogsAsync(AdminAuditLogListQuery query, CancellationToken cancellationToken);

    /// <summary>Outcome of an admin account mutation against a target user.</summary>
    Task<AdminUserManagementOutcome> SetUserActiveAsync(Guid actorUserId, Guid targetUserId, bool isActive, CancellationToken cancellationToken);

    Task<AdminUserManagementOutcome> SetUserRolesAsync(Guid actorUserId, Guid targetUserId, IReadOnlyCollection<string> roles, bool actorIsSuperAdmin, CancellationToken cancellationToken);

    Task<AdminBroadcastResponse> BroadcastAsync(Guid actorUserId, AdminBroadcastRequest request, CancellationToken cancellationToken);
}

public enum AdminUserManagementOutcome
{
    NotFound,
    SelfTargetForbidden,
    PrivilegeEscalationForbidden,
    UnknownRole,
    NoRolesRequested,
    Updated
}
