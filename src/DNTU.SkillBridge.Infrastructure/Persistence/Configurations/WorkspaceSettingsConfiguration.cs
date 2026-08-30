using DNTU.SkillBridge.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceSettingsConfiguration : IEntityTypeConfiguration<WorkspaceSettings>
{
    public void Configure(EntityTypeBuilder<WorkspaceSettings> builder)
    {
        builder.ToTable("workspace_settings");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.WorkingAgreement).HasMaxLength(2000);
        builder.HasIndex(entity => entity.ProjectId).IsUnique();
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
