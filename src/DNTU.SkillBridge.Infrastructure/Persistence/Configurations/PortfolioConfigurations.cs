using DNTU.SkillBridge.Domain.Portfolio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class VerifiedSkillConfiguration : IEntityTypeConfiguration<VerifiedSkill>
{
    public void Configure(EntityTypeBuilder<VerifiedSkill> builder)
    {
        builder.ToTable("verified_skills");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Level).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.VerifiedAt).IsRequired();
        builder.HasIndex(entity => new { entity.StudentId, entity.SkillId, entity.ProjectId }).IsUnique();
        builder.HasIndex(entity => new { entity.StudentId, entity.IsRevoked });
    }
}

public sealed class PortfolioEntryConfiguration : IEntityTypeConfiguration<PortfolioEntry>
{
    public void Configure(EntityTypeBuilder<PortfolioEntry> builder)
    {
        builder.ToTable("portfolio_entries");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Summary).HasMaxLength(4000).IsRequired();
        builder.HasIndex(entity => new { entity.StudentId, entity.ProjectId }).IsUnique();
        builder.HasIndex(entity => new { entity.StudentId, entity.IsPublished });
    }
}

public sealed class SkillVerificationRequestConfiguration : IEntityTypeConfiguration<SkillVerificationRequest>
{
    public void Configure(EntityTypeBuilder<SkillVerificationRequest> builder)
    {
        builder.ToTable("skill_verification_requests");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Level).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.EvidenceUrl).HasMaxLength(1000);
        builder.Property(entity => entity.EvidenceFileUrlsJson).HasMaxLength(4000);
        builder.Property(entity => entity.StudentNote).HasMaxLength(2000);
        builder.Property(entity => entity.LecturerFeedback).HasMaxLength(2000);
        builder.HasIndex(entity => new { entity.StudentId, entity.Status });
        builder.HasIndex(entity => new { entity.AssignedLecturerId, entity.Status });
        builder.HasIndex(entity => entity.SkillId);
    }
}
