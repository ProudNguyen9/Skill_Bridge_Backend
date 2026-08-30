using DNTU.SkillBridge.Domain.Commitments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawalRequest> builder)
    {
        builder.ToTable("withdrawal_requests");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.LecturerNote).HasMaxLength(1000);
        builder.Property(entity => entity.DecisionNote).HasMaxLength(1000);
        builder.HasIndex(entity => new { entity.StudentId, entity.Status, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.ProjectId, entity.Status, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.ProjectId, entity.StudentId })
            .HasFilter("\"Status\" IN ('REQUESTED', 'RECOMMENDED')")
            .IsUnique();
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
