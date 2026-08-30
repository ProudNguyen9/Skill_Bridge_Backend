using DNTU.SkillBridge.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Slug).HasMaxLength(220).IsRequired();
        builder.Property(entity => entity.Category).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => entity.NormalizedName).IsUnique();
        builder.HasIndex(entity => entity.Slug).IsUnique();
        builder.HasIndex(entity => new { entity.IsActive, entity.Name });
    }
}

public sealed class IndustryConfiguration : IEntityTypeConfiguration<Industry>
{
    public void Configure(EntityTypeBuilder<Industry> builder)
    {
        builder.ToTable("industries");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Slug).HasMaxLength(220).IsRequired();
        builder.HasIndex(entity => entity.NormalizedName).IsUnique();
        builder.HasIndex(entity => entity.Slug).IsUnique();
        builder.HasIndex(entity => new { entity.IsActive, entity.Name });
    }
}

public sealed class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
{
    public void Configure(EntityTypeBuilder<Faculty> builder)
    {
        builder.ToTable("faculties");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.NormalizedName).HasMaxLength(200).IsRequired();
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => entity.NormalizedName).IsUnique();
        builder.HasIndex(entity => new { entity.IsActive, entity.Name });
    }
}

public sealed class MajorConfiguration : IEntityTypeConfiguration<Major>
{
    public void Configure(EntityTypeBuilder<Major> builder)
    {
        builder.ToTable("majors");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.NormalizedName).HasMaxLength(200).IsRequired();
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => new { entity.FacultyId, entity.NormalizedName }).IsUnique();
        builder.HasIndex(entity => new { entity.IsActive, entity.Name });
        builder.HasOne(entity => entity.Faculty)
            .WithMany()
            .HasForeignKey(entity => entity.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("banks");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Bin).HasMaxLength(20).IsRequired();
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => entity.NormalizedName).IsUnique();
        builder.HasIndex(entity => entity.Bin).IsUnique();
        builder.HasIndex(entity => new { entity.IsActive, entity.Name });
    }
}
