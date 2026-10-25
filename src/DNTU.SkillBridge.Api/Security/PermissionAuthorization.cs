using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace DNTU.SkillBridge.Api.Security;

/// <summary>
/// Yêu cầu user hiện tại phải có permission cụ thể (claim "permission" nhúng trong JWT).
/// SUPER_ADMIN được phép qua mọi gate có permission.
/// </summary>
public sealed class PermissionRequirement(string permissionName) : IAuthorizationRequirement
{
    public string PermissionName { get; } = permissionName;
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (context.User.IsInRole(RoleNames.SuperAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var hasPermission = context.User.Claims.Any(claim =>
            claim.Type == "permission" &&
            string.Equals(claim.Value, requirement.PermissionName, StringComparison.OrdinalIgnoreCase));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Tên policy sinh từ permission: "permission:projects.approve".</summary>
public static class AuthorizationPolicies
{
    private const string Prefix = "permission:";

    public static string For(string permissionName) => $"{Prefix}{permissionName}";
}
