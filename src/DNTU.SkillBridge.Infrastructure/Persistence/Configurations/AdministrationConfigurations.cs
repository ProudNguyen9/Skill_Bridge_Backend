using DNTU.SkillBridge.Domain.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Action).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.CorrelationId).HasMaxLength(100);
        builder.Property(entity => entity.IpHash).HasMaxLength(128);
        builder.Property(entity => entity.UserAgent).HasMaxLength(500);
        builder.Property(entity => entity.MetadataJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(entity => new { entity.ActorUserId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.Action, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.EntityType, entity.EntityId });
        builder.HasIndex(entity => entity.ProjectId);
    }
}

public sealed class AdminPolicySettingConfiguration : IEntityTypeConfiguration<AdminPolicySetting>
{
    public void Configure(EntityTypeBuilder<AdminPolicySetting> builder)
    {
        builder.ToTable("admin_policy_settings");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Category).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.SettingsJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(entity => entity.Category).IsUnique();
    }
}
