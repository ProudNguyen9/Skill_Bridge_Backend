using DNTU.SkillBridge.Domain.Academics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(300).IsRequired();
        builder.HasIndex(entity => entity.Code).IsUnique();
    }
}

public sealed class CourseProjectConfiguration : IEntityTypeConfiguration<CourseProject>
{
    public void Configure(EntityTypeBuilder<CourseProject> builder)
    {
        builder.ToTable("course_projects");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(entity => new { entity.ProjectId, entity.Status });
        builder.HasIndex(entity => new { entity.CourseId, entity.ProjectId, entity.Status });
        builder.HasOne<Course>().WithMany().HasForeignKey(entity => entity.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Rubric>().WithMany().HasForeignKey(entity => entity.RubricId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RubricConfiguration : IEntityTypeConfiguration<Rubric>
{
    public void Configure(EntityTypeBuilder<Rubric> builder)
    {
        builder.ToTable("rubrics");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Version).IsConcurrencyToken();
        builder.HasIndex(entity => new { entity.LecturerId, entity.IsLocked });
        builder.HasMany(entity => entity.Criteria)
            .WithOne()
            .HasForeignKey(entity => entity.RubricId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RubricCriterionConfiguration : IEntityTypeConfiguration<RubricCriterion>
{
    public void Configure(EntityTypeBuilder<RubricCriterion> builder)
    {
        builder.ToTable("rubric_criteria");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Weight).HasPrecision(5, 2);
        builder.HasIndex(entity => new { entity.RubricId, entity.SortOrder }).IsUnique();
    }
}
