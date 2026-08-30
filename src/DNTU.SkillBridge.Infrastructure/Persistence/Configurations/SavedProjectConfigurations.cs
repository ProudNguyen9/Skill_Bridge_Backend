using DNTU.SkillBridge.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class SavedProjectConfiguration : IEntityTypeConfiguration<SavedProject>
{
    public void Configure(EntityTypeBuilder<SavedProject> builder)
    {
        builder.ToTable("saved_projects");
        builder.HasKey(entity => new { entity.StudentId, entity.ProjectId });
        builder.HasOne<DNTU.SkillBridge.Domain.Students.StudentProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entity => new { entity.StudentId, entity.CreatedAt });
    }
}
