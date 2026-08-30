using DNTU.SkillBridge.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectApprovalConfiguration : IEntityTypeConfiguration<ProjectApproval>
{
    public void Configure(EntityTypeBuilder<ProjectApproval> builder)
    {
        builder.ToTable("project_approvals");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Note).HasMaxLength(1000);
        builder.HasIndex(entity => new { entity.ProjectId, entity.DecidedAt });
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DNTU.SkillBridge.Domain.Identity.User>()
            .WithMany()
            .HasForeignKey(entity => entity.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
