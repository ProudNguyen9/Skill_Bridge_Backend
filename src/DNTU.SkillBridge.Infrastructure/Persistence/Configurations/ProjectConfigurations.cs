using DNTU.SkillBridge.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.NormalizedTitle).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Slug).HasMaxLength(220).IsRequired();
        builder.Property(entity => entity.Summary).HasMaxLength(500);
        builder.Property(entity => entity.ProblemStatement).HasMaxLength(4000);
        builder.Property(entity => entity.BusinessRequirements).HasMaxLength(4000);
        builder.Property(entity => entity.TechnicalConstraints).HasMaxLength(4000);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.AllowanceCurrency).HasMaxLength(3);
        builder.HasIndex(entity => entity.Slug).IsUnique();
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => new { entity.CompanyId, entity.Status });
        builder.HasIndex(entity => entity.ApplicationDeadline);
        builder.HasIndex(entity => new { entity.Status, entity.IsActive });
        builder.HasIndex(entity => entity.CreatedAt);
        builder.HasOne(entity => entity.Company)
            .WithMany()
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DNTU.SkillBridge.Domain.Catalog.Industry>()
            .WithMany()
            .HasForeignKey(entity => entity.IndustryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entity => entity.Skills)
            .WithOne(skill => skill.Project)
            .HasForeignKey(skill => skill.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Deliverables)
            .WithOne(deliverable => deliverable.Project)
            .HasForeignKey(deliverable => deliverable.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectSkillConfiguration : IEntityTypeConfiguration<ProjectSkill>
{
    public void Configure(EntityTypeBuilder<ProjectSkill> builder)
    {
        builder.ToTable("project_skills");
        builder.HasKey(entity => new { entity.ProjectId, entity.SkillId });
        builder.Property(entity => entity.RequirementLevel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasOne<DNTU.SkillBridge.Domain.Catalog.Skill>()
            .WithMany()
            .HasForeignKey(entity => entity.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectDeliverableConfiguration : IEntityTypeConfiguration<ProjectDeliverable>
{
    public void Configure(EntityTypeBuilder<ProjectDeliverable> builder)
    {
        builder.ToTable("project_deliverables");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.HasIndex(entity => new { entity.ProjectId, entity.SortOrder });
    }
}
