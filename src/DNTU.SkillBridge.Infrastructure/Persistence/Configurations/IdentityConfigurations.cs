using DNTU.SkillBridge.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Email).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.PasswordHash).HasMaxLength(1024).IsRequired();
        builder.HasIndex(entity => entity.NormalizedEmail).IsUnique();
        builder.HasMany(entity => entity.UserRoles).WithOne(entity => entity.User).HasForeignKey(entity => entity.UserId);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.NormalizedName).HasMaxLength(100).IsRequired();
        builder.HasIndex(entity => entity.NormalizedName).IsUnique();
        builder.HasMany(entity => entity.RolePermissions).WithOne(entity => entity.Role).HasForeignKey(entity => entity.RoleId);
    }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(150).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(500).IsRequired();
        builder.HasIndex(entity => entity.Name).IsUnique();
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(entity => new { entity.UserId, entity.RoleId });
        builder.HasOne(entity => entity.Role).WithMany().HasForeignKey(entity => entity.RoleId);
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(entity => new { entity.RoleId, entity.PermissionId });
        builder.HasOne(entity => entity.Permission).WithMany().HasForeignKey(entity => entity.PermissionId);
    }
}
