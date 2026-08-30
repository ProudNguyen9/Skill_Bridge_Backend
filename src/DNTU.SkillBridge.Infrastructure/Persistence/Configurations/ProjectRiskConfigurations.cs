using DNTU.SkillBridge.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectRiskSnapshotConfiguration : IEntityTypeConfiguration<ProjectRiskSnapshot>
{
    public void Configure(EntityTypeBuilder<ProjectRiskSnapshot> builder)
    {
        builder.ToTable("project_risk_snapshots");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Level).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.ReasonsJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(entity => entity.ProjectId).IsUnique();
        builder.HasIndex(entity => new { entity.Level, entity.CalculatedAt });
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>().WithMany().HasForeignKey(entity => entity.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectRiskHistoryConfiguration : IEntityTypeConfiguration<ProjectRiskHistory>
{
    public void Configure(EntityTypeBuilder<ProjectRiskHistory> builder)
    {
        builder.ToTable("project_risk_history");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Level).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.ReasonsJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(entity => new { entity.ProjectId, entity.CalculatedAt });
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>().WithMany().HasForeignKey(entity => entity.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
