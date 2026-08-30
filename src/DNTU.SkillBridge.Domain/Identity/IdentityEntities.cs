using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Identity;

public sealed class User : AuditableEntity
{
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool EmailVerified { get; private set; }
    public bool IsActive { get; private set; } = true;
    public ICollection<UserRole> UserRoles { get; } = [];

    private User() { }

    public User(string email, string displayName, string passwordHash)
    {
        Email = email.Trim();
        NormalizedEmail = email.Trim().ToUpperInvariant();
        DisplayName = displayName.Trim();
        PasswordHash = passwordHash;
    }

    public void AssignRole(Guid roleId) => UserRoles.Add(new UserRole(Id, roleId));

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void SetEmailVerified() => EmailVerified = true;

    public void Deactivate() => IsActive = false;
}

public sealed class Role : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; } = [];

    private Role() { }

    public Role(string name)
    {
        Name = name.Trim();
        NormalizedName = name.Trim().ToUpperInvariant();
    }
}

public sealed class Permission : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    private Permission() { }

    public Permission(string name, string description)
    {
        Name = name.Trim();
        Description = description.Trim();
    }
}

public sealed class UserRole
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;

    private UserRole() { }

    public UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }
}

public sealed class RolePermission
{
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;
    public Guid PermissionId { get; private set; }
    public Permission Permission { get; private set; } = null!;
}

public static class RoleNames
{
    public const string Student = "STUDENT";
    public const string Company = "COMPANY";
    public const string Lecturer = "LECTURER";
    public const string Admin = "ADMIN";
    public const string SuperAdmin = "SUPER_ADMIN";
}
