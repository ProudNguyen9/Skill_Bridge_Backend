using DNTU.SkillBridge.Domain.Lecturers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class LecturerProfileConfiguration : IEntityTypeConfiguration<LecturerProfile>
{
    public void Configure(EntityTypeBuilder<LecturerProfile> builder)
    {
        builder.ToTable("lecturer_profiles");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.LecturerCode).HasMaxLength(32);
        builder.Property(entity => entity.Department).HasMaxLength(200);
        builder.Property(entity => entity.AcademicTitle).HasMaxLength(100);
        builder.Property(entity => entity.Bio).HasMaxLength(1000);
        builder.Property(entity => entity.WebsiteUrl).HasMaxLength(500);
        builder.Property(entity => entity.OfficeLocation).HasMaxLength(200);
        builder.Property(entity => entity.PhoneNumber).HasMaxLength(20);
        builder.Property(entity => entity.AvatarUrl).HasMaxLength(500);
        builder.Property(entity => entity.ApprovalStatus).HasDefaultValue(LecturerApprovalStatus.PENDING);
        builder.Property(entity => entity.ReviewNote).HasMaxLength(1000);
        builder.HasIndex(entity => entity.UserId).IsUnique();
        builder.HasIndex(entity => entity.LecturerCode).IsUnique();
        builder.HasOne<DNTU.SkillBridge.Domain.Identity.User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Assignments)
            .WithOne()
            .HasForeignKey(entity => entity.LecturerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LecturerAssignmentConfiguration : IEntityTypeConfiguration<LecturerAssignment>
{
    public void Configure(EntityTypeBuilder<LecturerAssignment> builder)
    {
        builder.ToTable("lecturer_assignments");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Note).HasMaxLength(1000);
        builder.Property(entity => entity.ExpectedWork).HasMaxLength(2000);
        builder.Property(entity => entity.ExpectedDuration).HasMaxLength(200);
        builder.Property(entity => entity.CourseName).HasMaxLength(200);
        builder.Property(entity => entity.DeclineReason).HasMaxLength(1000);
        // One active/invited supervising lecturer per project; declined/ended assignments allow re-inviting.
        builder.HasIndex(entity => entity.ProjectId)
            .HasDatabaseName("IX_lecturer_assignments_ProjectId_ActiveOrInvited")
            .IsUnique()
            .HasFilter("[Status] IN ('INVITED', 'ACTIVE')");
        builder.HasIndex(entity => entity.LecturerId);
        builder.HasIndex(entity => entity.TeamId);
    }
}
