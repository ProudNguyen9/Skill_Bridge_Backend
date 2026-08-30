using DNTU.SkillBridge.Domain.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class TechnicalSubmissionReviewConfiguration : IEntityTypeConfiguration<TechnicalSubmissionReview>
{
    public void Configure(EntityTypeBuilder<TechnicalSubmissionReview> builder)
    {
        builder.ToTable("technical_submission_reviews");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Decision).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Feedback).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.CriteriaNotes).HasMaxLength(8000);
        builder.HasIndex(entity => new { entity.SubmissionId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.ReviewerUserId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.SubmissionId, entity.SubmissionVersionId, entity.Decision });
        builder.HasOne<ProjectSubmission>()
            .WithMany()
            .HasForeignKey(entity => entity.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SubmissionVersion>()
            .WithMany()
            .HasForeignKey(entity => entity.SubmissionVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
