using DNTU.SkillBridge.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(100).IsRequired(); builder.Property(x => x.Title).HasMaxLength(300).IsRequired(); builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
        builder.HasOne<DNTU.SkillBridge.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences"); builder.HasKey(x => x.Id); builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasOne<DNTU.SkillBridge.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages"); builder.HasKey(x => x.Id); builder.Property(x => x.Type).HasMaxLength(100).IsRequired(); builder.Property(x => x.PayloadJson).HasMaxLength(10000); builder.Property(x => x.LastError).HasMaxLength(500);
        builder.HasIndex(x => new { x.ProcessedAt, x.AvailableAt });
    }
}