using DNTU.SkillBridge.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectCompletionConfiguration : IEntityTypeConfiguration<ProjectCompletion>
{
    public void Configure(EntityTypeBuilder<ProjectCompletion> builder)
    {
        builder.ToTable("project_completions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.EvidenceJson).HasMaxLength(10000).IsRequired();
        builder.HasIndex(entity => entity.ProjectId).IsUnique();
        builder.HasOne<Project>().WithMany().HasForeignKey(entity => entity.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
