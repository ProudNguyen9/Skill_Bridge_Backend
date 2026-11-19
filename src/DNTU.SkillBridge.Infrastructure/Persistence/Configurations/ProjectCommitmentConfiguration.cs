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
        builder.Property(entity => entity.ProposedRole).HasMaxLength(100);
        builder.Property(entity => entity.Conditions).HasMaxLength(2000);
        builder.Property(entity => entity.StudentSignerName).HasMaxLength(150);
        builder.Property(entity => entity.DeclineReason).HasMaxLength(1000);
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
