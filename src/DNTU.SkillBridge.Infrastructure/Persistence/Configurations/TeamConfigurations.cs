using DNTU.SkillBridge.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.AvatarUrl).HasMaxLength(1000);
        builder.Property(entity => entity.TeamType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(TeamType.PROJECT).IsRequired();
        builder.HasIndex(entity => entity.CreatedByStudentId);
        builder.HasMany(entity => entity.Members)
            .WithOne(entity => entity.Team)
            .HasForeignKey(entity => entity.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("team_members");
        builder.HasKey(entity => new { entity.TeamId, entity.StudentId });
        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.FunctionalRole).HasMaxLength(100);
        builder.HasIndex(entity => entity.StudentId);
        builder.HasOne(entity => entity.Student)
            .WithMany()
            .HasForeignKey(entity => entity.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TeamInvitationConfiguration : IEntityTypeConfiguration<TeamInvitation>
{
    public void Configure(EntityTypeBuilder<TeamInvitation> builder)
    {
        builder.ToTable("team_invitations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.FunctionalRole).HasMaxLength(100);
        builder.Property(entity => entity.Message).HasMaxLength(500);
        builder.HasIndex(entity => new { entity.TeamId, entity.InvitedStudentId, entity.Status });
        builder.HasIndex(entity => new { entity.InvitedStudentId, entity.Status, entity.ExpiresAt });
        builder.HasOne(entity => entity.Team)
            .WithMany()
            .HasForeignKey(entity => entity.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.InvitedStudent)
            .WithMany()
            .HasForeignKey(entity => entity.InvitedStudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TeamProjectRecommendationConfiguration : IEntityTypeConfiguration<TeamProjectRecommendation>
{
    public void Configure(EntityTypeBuilder<TeamProjectRecommendation> builder)
    {
        builder.ToTable("team_project_recommendations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.ResponseNote).HasMaxLength(500);
        builder.HasIndex(entity => new { entity.TeamId, entity.ProjectId });
        builder.HasIndex(entity => new { entity.LecturerId, entity.Status });
        builder.HasOne(entity => entity.Lecturer)
            .WithMany()
            .HasForeignKey(entity => entity.LecturerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.Team)
            .WithMany()
            .HasForeignKey(entity => entity.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.Project)
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
