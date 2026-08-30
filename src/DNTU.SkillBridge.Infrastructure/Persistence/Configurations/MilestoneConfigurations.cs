using DNTU.SkillBridge.Domain.Milestones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectMilestoneConfiguration : IEntityTypeConfiguration<ProjectMilestone>
{
    public void Configure(EntityTypeBuilder<ProjectMilestone> builder)
    {
        builder.ToTable("project_milestones");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(4000);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Version).IsConcurrencyToken();
        builder.HasIndex(entity => new { entity.ProjectId, entity.Sequence }).IsUnique();
        builder.HasIndex(entity => new { entity.ProjectId, entity.Status, entity.DueAt });
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>().WithMany().HasForeignKey(entity => entity.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MilestoneDeliverableConfiguration : IEntityTypeConfiguration<MilestoneDeliverable>
{
    public void Configure(EntityTypeBuilder<MilestoneDeliverable> builder)
    {
        builder.ToTable("milestone_deliverables");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Criteria).HasMaxLength(2000);
        builder.HasIndex(entity => entity.MilestoneId);
        builder.HasOne<ProjectMilestone>().WithMany().HasForeignKey(entity => entity.MilestoneId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DNTU.SkillBridge.Domain.Files.FileRecord>().WithMany().HasForeignKey(entity => entity.FileId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class MilestoneApprovalHistoryConfiguration : IEntityTypeConfiguration<MilestoneApprovalHistory>
{
    public void Configure(EntityTypeBuilder<MilestoneApprovalHistory> builder)
    {
        builder.ToTable("milestone_approval_history");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.FromStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.ToStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Note).HasMaxLength(2000);
        builder.HasIndex(entity => new { entity.MilestoneId, entity.CreatedAt });
        builder.HasOne<ProjectMilestone>().WithMany().HasForeignKey(entity => entity.MilestoneId).OnDelete(DeleteBehavior.Cascade);
    }
}
