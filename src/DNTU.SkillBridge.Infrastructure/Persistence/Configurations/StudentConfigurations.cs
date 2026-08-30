using DNTU.SkillBridge.Domain;
using DNTU.SkillBridge.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.ToTable("student_profiles");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.StudentCode).HasMaxLength(32);
        builder.Property(entity => entity.AcademicYear).HasMaxLength(16);
        builder.Property(entity => entity.PhoneNumber).HasMaxLength(20);
        builder.Property(entity => entity.Bio).HasMaxLength(1000);
        builder.Property(entity => entity.GithubUrl).HasMaxLength(500);
        builder.Property(entity => entity.LinkedinUrl).HasMaxLength(500);
        builder.Property(entity => entity.PortfolioUrl).HasMaxLength(500);
        builder.Property(entity => entity.CvUrl).HasMaxLength(500);
        builder.HasIndex(entity => entity.UserId).IsUnique();
        builder.HasIndex(entity => entity.StudentCode).IsUnique();
        builder.HasOne(entity => entity.Privacy)
            .WithOne()
            .HasForeignKey<StudentPrivacySettings>(settings => settings.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DNTU.SkillBridge.Domain.Catalog.Faculty>()
            .WithMany()
            .HasForeignKey(entity => entity.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DNTU.SkillBridge.Domain.Catalog.Major>()
            .WithMany()
            .HasForeignKey(entity => entity.MajorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StudentPrivacySettingsConfiguration : IEntityTypeConfiguration<StudentPrivacySettings>
{
    public void Configure(EntityTypeBuilder<StudentPrivacySettings> builder)
    {
        builder.ToTable("student_privacy_settings");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => entity.StudentProfileId).IsUnique();
    }
}

public sealed class StudentSkillConfiguration : IEntityTypeConfiguration<StudentSkill>
{
    public void Configure(EntityTypeBuilder<StudentSkill> builder)
    {
        builder.ToTable("student_skills");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Level).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(entity => new { entity.StudentId, entity.SkillId }).IsUnique();
        builder.HasIndex(entity => entity.StudentId);
        builder.HasOne<DNTU.SkillBridge.Domain.Catalog.Skill>()
            .WithMany()
            .HasForeignKey(entity => entity.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DNTU.SkillBridge.Domain.Students.StudentProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StudentCertificateConfiguration : IEntityTypeConfiguration<StudentCertificate>
{
    public void Configure(EntityTypeBuilder<StudentCertificate> builder)
    {
        builder.ToTable("student_certificates");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Issuer).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.CertificateUrl).HasMaxLength(500);
        builder.HasIndex(entity => new { entity.StudentId, entity.Name });
        builder.HasOne<DNTU.SkillBridge.Domain.Students.StudentProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
