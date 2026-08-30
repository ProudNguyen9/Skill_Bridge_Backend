using DNTU.SkillBridge.Domain.Academics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class AcademicEvaluationConfiguration : IEntityTypeConfiguration<AcademicEvaluation>
{
    public void Configure(EntityTypeBuilder<AcademicEvaluation> builder)
    {
        builder.ToTable("academic_evaluations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.TotalScore).HasPrecision(5, 2);
        builder.Property(entity => entity.Version).IsConcurrencyToken();
        builder.HasIndex(entity => new { entity.ProjectId, entity.StudentId, entity.RubricId }).IsUnique();
        builder.HasIndex(entity => new { entity.EvaluatorUserId, entity.Status });
        builder.HasMany(entity => entity.Scores).WithOne().HasForeignKey(entity => entity.EvaluationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AcademicCriterionScoreConfiguration : IEntityTypeConfiguration<AcademicCriterionScore>
{
    public void Configure(EntityTypeBuilder<AcademicCriterionScore> builder)
    {
        builder.ToTable("academic_criterion_scores");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Score).HasPrecision(5, 2);
        builder.Property(entity => entity.Weight).HasPrecision(5, 2);
        builder.HasIndex(entity => new { entity.EvaluationId, entity.RubricCriterionId }).IsUnique();
    }
}
