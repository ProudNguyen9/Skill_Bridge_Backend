using DNTU.SkillBridge.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Seed;

public static class IdentitySeed
{
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
    }
}
