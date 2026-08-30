using DNTU.SkillBridge.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("project_tasks");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(4000);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.Priority).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.Version).IsConcurrencyToken();
        builder.HasIndex(entity => new { entity.ProjectId, entity.Status, entity.SortOrder });
        builder.HasIndex(entity => new { entity.AssigneeStudentId, entity.DueAt });
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
