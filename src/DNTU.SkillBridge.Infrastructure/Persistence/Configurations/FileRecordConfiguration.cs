using DNTU.SkillBridge.Domain.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class FileRecordConfiguration : IEntityTypeConfiguration<FileRecord>
{
    public void Configure(EntityTypeBuilder<FileRecord> builder)
    {
        builder.ToTable("file_records");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.StorageKey).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.ChecksumSha256).HasMaxLength(64);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(entity => entity.StorageKey).IsUnique();
        builder.HasIndex(entity => new { entity.ProjectId, entity.Status });
        builder.HasIndex(entity => new { entity.UploadedByUserId, entity.Status, entity.ExpiresAt });
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>().WithMany().HasForeignKey(entity => entity.ProjectId).OnDelete(DeleteBehavior.SetNull);
    }
}
