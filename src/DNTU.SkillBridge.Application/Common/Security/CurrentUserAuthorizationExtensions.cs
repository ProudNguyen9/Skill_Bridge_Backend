using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;

namespace DNTU.SkillBridge.Application.Common.Security;

/// <summary>
/// Extension kiểm tra permission cho các chỗ cần check imperative (trong service/controller).
/// SUPER_ADMIN luôn có mọi permission.
/// </summary>
public static class CurrentUserAuthorizationExtensions
{
    public static bool HasPermission(this ICurrentUser user, string permission) =>
        user.IsAuthenticated &&
        (user.Roles.Contains(RoleNames.SuperAdmin) || user.Permissions.Contains(permission));

    /// <summary>Có ít nhất một permission trong danh sách.</summary>
    public static bool HasAnyPermission(this ICurrentUser user, params string[] permissions) =>
        permissions.Length != 0 && permissions.Any(user.HasPermission);
}
