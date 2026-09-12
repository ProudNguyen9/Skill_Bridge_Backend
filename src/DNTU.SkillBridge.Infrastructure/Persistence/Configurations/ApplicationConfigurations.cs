using DNTU.SkillBridge.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<DNTU.SkillBridge.Domain.Applications.Application>
{
    public void Configure(EntityTypeBuilder<DNTU.SkillBridge.Domain.Applications.Application> builder)
    {
        builder.ToTable("applications");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.CoverLetter).HasMaxLength(4000);
        builder.Property(entity => entity.WithdrawReason).HasMaxLength(1000);
        builder.Property(entity => entity.DecisionReason).HasMaxLength(1000);

        // One application per student per project — the final concurrency authority.
        builder.HasIndex(entity => new { entity.ProjectId, entity.StudentId }).IsUnique();
        // SQL Server enforces the cross-project selection rule even when two recruiters
        // race in separate requests: a student may have only one accepted application.
        builder.HasIndex(entity => entity.StudentId)
            .HasDatabaseName("ux_applications_student_accepted")
            .IsUnique()
            .HasFilter("[Status] = 'ACCEPTED'");
        builder.HasIndex(entity => new { entity.StudentId, entity.Status });
        builder.HasIndex(entity => new { entity.ProjectId, entity.Status });
        builder.HasIndex(entity => entity.TeamId);

        builder.HasOne(entity => entity.Student)
            .WithMany()
            .HasForeignKey(entity => entity.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.Project)
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Team)
            .WithMany()
            .HasForeignKey(entity => entity.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
