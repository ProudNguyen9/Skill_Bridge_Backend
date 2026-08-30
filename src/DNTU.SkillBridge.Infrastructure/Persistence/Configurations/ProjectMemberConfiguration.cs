using DNTU.SkillBridge.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("project_members");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(entity => new { entity.ProjectId, entity.StudentId }).IsUnique();
        builder.HasIndex(entity => new { entity.StudentId, entity.IsActive });
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>().WithMany().HasForeignKey(entity => entity.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Student).WithMany().HasForeignKey(entity => entity.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
