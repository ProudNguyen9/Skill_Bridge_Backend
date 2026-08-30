using DNTU.SkillBridge.Domain.Commitments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectCommitmentConfiguration : IEntityTypeConfiguration<ProjectCommitment>
{
    public void Configure(EntityTypeBuilder<ProjectCommitment> builder)
    {
        builder.ToTable("project_commitments");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.PolicyVersion).HasMaxLength(32).IsRequired();
        builder.HasIndex(entity => new { entity.ProjectId, entity.StudentId }).IsUnique();
        builder.HasIndex(entity => new { entity.StudentId, entity.Status });
        builder.HasIndex(entity => new { entity.ProjectId, entity.Status });
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DNTU.SkillBridge.Domain.Students.StudentProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
