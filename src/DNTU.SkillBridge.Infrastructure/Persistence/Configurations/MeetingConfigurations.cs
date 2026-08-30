using DNTU.SkillBridge.Domain.Meetings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectMeetingConfiguration : IEntityTypeConfiguration<ProjectMeeting>
{
    public void Configure(EntityTypeBuilder<ProjectMeeting> builder)
    {
        builder.ToTable("project_meetings");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(4000);
        builder.Property(entity => entity.ExternalUrl).HasMaxLength(1000);
        builder.Property(entity => entity.ExternalLinkType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ReminderState).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(entity => new { entity.ProjectId, entity.StartAt });
        builder.HasOne<DNTU.SkillBridge.Domain.Projects.Project>().WithMany().HasForeignKey(entity => entity.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MeetingParticipantConfiguration : IEntityTypeConfiguration<MeetingParticipant>
{
    public void Configure(EntityTypeBuilder<MeetingParticipant> builder)
    {
        builder.ToTable("meeting_participants");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.AttendanceStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(entity => new { entity.MeetingId, entity.StudentId }).IsUnique();
        builder.HasOne<ProjectMeeting>().WithMany().HasForeignKey(entity => entity.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DNTU.SkillBridge.Domain.Students.StudentProfile>().WithMany().HasForeignKey(entity => entity.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
