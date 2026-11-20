using DNTU.SkillBridge.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class GoogleCalendarIntegrationConfiguration : IEntityTypeConfiguration<GoogleCalendarIntegration>
{
    public void Configure(EntityTypeBuilder<GoogleCalendarIntegration> builder)
    {
        builder.ToTable("google_calendar_integrations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ConnectedEmail).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.RefreshToken).HasMaxLength(2000).IsRequired();
        builder.HasIndex(entity => entity.UserId).IsUnique();
        builder.HasOne<DNTU.SkillBridge.Domain.Identity.User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
