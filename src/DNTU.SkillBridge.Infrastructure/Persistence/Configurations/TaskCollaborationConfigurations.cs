using DNTU.SkillBridge.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("task_comments");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Content).HasMaxLength(4000).IsRequired();
        builder.HasIndex(entity => new { entity.ProjectTaskId, entity.CreatedAt });
        builder.HasOne<ProjectTask>().WithMany().HasForeignKey(entity => entity.ProjectTaskId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TaskChecklistItemConfiguration : IEntityTypeConfiguration<TaskChecklistItem>
{
    public void Configure(EntityTypeBuilder<TaskChecklistItem> builder)
    {
        builder.ToTable("task_checklist_items");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(300).IsRequired();
        builder.HasIndex(entity => new { entity.ProjectTaskId, entity.SortOrder });
        builder.HasOne<ProjectTask>().WithMany().HasForeignKey(entity => entity.ProjectTaskId).OnDelete(DeleteBehavior.Cascade);
    }
}
