using DNTU.SkillBridge.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Seed;

public static class IdentitySeed
{
    // Ma trận Role -> Permissions: nguồn duy nhất quyết định role nào có quyền gì.
    // ADMIN/SUPER_ADMIN mặc định có toàn bộ permission (xem GetPermissionsForRole).
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RolePermissionMatrix =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [RoleNames.Student] = new[]
            {
                PermissionNames.FilesUpload,
                PermissionNames.FilesRead,
                PermissionNames.ProjectsApply,
                PermissionNames.SubmissionsCreate,
            },
            [RoleNames.Company] = new[]
            {
                PermissionNames.FilesUpload,
                PermissionNames.FilesRead,
                PermissionNames.ProjectsCreate,
                PermissionNames.ApplicationsReview,
                PermissionNames.SubmissionsReviewBusiness,
                PermissionNames.PaymentsView,
            },
            [RoleNames.Lecturer] = new[]
            {
                PermissionNames.FilesUpload,
                PermissionNames.FilesRead,
                PermissionNames.ProjectsApprove,
                PermissionNames.SubmissionsReviewTechnical,
                PermissionNames.RubricsManage,
                PermissionNames.EvaluationsManage,
                PermissionNames.SkillsVerify,
            },
        };

    public static IReadOnlyList<string> GetPermissionsForRole(string roleName)
    {
        if (roleName == RoleNames.Admin || roleName == RoleNames.SuperAdmin)
        {
            return typeof(PermissionNames)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Select(field => field.GetValue(null)?.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToList();
        }

        return RolePermissionMatrix.TryGetValue(roleName, out var permissions)
            ? permissions
            : Array.Empty<string>();
    }

    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var roles = new[] { RoleNames.Student, RoleNames.Company, RoleNames.Lecturer, RoleNames.Admin, RoleNames.SuperAdmin };
        foreach (var roleName in roles.Where(roleName => !dbContext.Roles.Any(role => role.NormalizedName == roleName)))
        {
            dbContext.Roles.Add(new Role(roleName));
        }

        var permissions = typeof(PermissionNames)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(field => field.GetValue(null)?.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>();

        foreach (var permissionName in permissions.Where(permissionName => !dbContext.Permissions.Any(permission => permission.Name == permissionName)))
        {
            dbContext.Permissions.Add(new Permission(permissionName, permissionName));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Ma trận RolePermissions: gán theo RolePermissionMatrix, ADMIN/SUPER_ADMIN được toàn bộ.
        foreach (var roleName in roles)
        {
            var role = dbContext.Roles.Include(role => role.RolePermissions)
                .FirstOrDefault(role => role.NormalizedName == roleName);
            if (role is null)
            {
                continue;
            }

            var permissionIds = dbContext.Permissions
                .Where(permission => GetPermissionsForRole(roleName).Contains(permission.Name))
                .Select(permission => permission.Id)
                .ToHashSet();

            foreach (var permissionId in permissionIds.Where(permissionId =>
                         role.RolePermissions.All(rolePermission => rolePermission.PermissionId != permissionId)))
            {
                role.RolePermissions.Add(new RolePermission(role.Id, permissionId));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
