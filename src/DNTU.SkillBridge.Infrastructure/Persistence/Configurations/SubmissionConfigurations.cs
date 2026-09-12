using DNTU.SkillBridge.Domain.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectSubmissionConfiguration : IEntityTypeConfiguration<ProjectSubmission>
{
    public void Configure(EntityTypeBuilder<ProjectSubmission> builder)
    {
        builder.ToTable("project_submissions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Version).IsConcurrencyToken();
        builder.HasIndex(entity => new { entity.ProjectId, entity.Status });
        builder.HasIndex(entity => new { entity.MilestoneId, entity.Status });
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DNTU.SkillBridge.Domain.Milestones.ProjectMilestone>()
            .WithMany()
            .HasForeignKey(entity => entity.MilestoneId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}

public sealed class SubmissionVersionConfiguration : IEntityTypeConfiguration<SubmissionVersion>
{
    public void Configure(EntityTypeBuilder<SubmissionVersion> builder)
    {
        builder.ToTable("submission_versions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Summary).HasMaxLength(4000);
        builder.Property(entity => entity.GithubUrl).HasMaxLength(1000);
        builder.Property(entity => entity.DemoUrl).HasMaxLength(1000);
        builder.Property(entity => entity.VideoUrl).HasMaxLength(1000);
        builder.HasIndex(entity => new { entity.SubmissionId, entity.VersionNumber }).IsUnique();
        builder.HasOne<ProjectSubmission>()
            .WithMany(entity => entity.Versions)
            .HasForeignKey(entity => entity.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DNTU.SkillBridge.Domain.Files.FileRecord>()
            .WithMany()
            .HasForeignKey(entity => entity.FileId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}

public sealed class SubmissionStatusHistoryConfiguration : IEntityTypeConfiguration<SubmissionStatusHistory>
{
    public void Configure(EntityTypeBuilder<SubmissionStatusHistory> builder)
    {
        builder.ToTable("submission_status_history");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.FromStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ToStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(entity => new { entity.SubmissionId, entity.CreatedAt });
        builder.HasOne<ProjectSubmission>().WithMany().HasForeignKey(entity => entity.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
