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
