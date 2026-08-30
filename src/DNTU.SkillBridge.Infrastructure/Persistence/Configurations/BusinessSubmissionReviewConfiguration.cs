using DNTU.SkillBridge.Domain.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class BusinessSubmissionReviewConfiguration : IEntityTypeConfiguration<BusinessSubmissionReview>
{
    public void Configure(EntityTypeBuilder<BusinessSubmissionReview> builder)
    {
        builder.ToTable("business_submission_reviews");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Decision).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.RequirementsFeedback).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.CollaborationFeedback).HasMaxLength(4000).IsRequired();
        builder.HasIndex(entity => new { entity.SubmissionId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.CompanyId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.SubmissionId, entity.SubmissionVersionId, entity.Decision });
        builder.HasOne<ProjectSubmission>()
            .WithMany()
            .HasForeignKey(entity => entity.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SubmissionVersion>()
            .WithMany()
            .HasForeignKey(entity => entity.SubmissionVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DNTU.SkillBridge.Domain.Companies.Company>()
            .WithMany()
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DNTU.SkillBridge.Domain.Identity.User>()
            .WithMany()
            .HasForeignKey(entity => entity.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
