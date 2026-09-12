using DNTU.SkillBridge.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectActivityConfiguration : IEntityTypeConfiguration<ProjectActivity>
{
    public void Configure(EntityTypeBuilder<ProjectActivity> builder)
    {
        builder.ToTable("project_activities");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.EventType).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.MetadataJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(entity => new { entity.ProjectId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.ProjectId, entity.EventType, entity.CreatedAt });
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>().WithMany().HasForeignKey(entity => entity.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
