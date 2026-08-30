using DNTU.SkillBridge.Domain.Meetings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class MeetingMinuteConfiguration : IEntityTypeConfiguration<MeetingMinute>
{
    public void Configure(EntityTypeBuilder<MeetingMinute> builder)
    {
        builder.ToTable("meeting_minutes");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Content).HasMaxLength(10000).IsRequired();
        builder.HasIndex(entity => entity.MeetingId).IsUnique();
        builder.HasOne<ProjectMeeting>().WithMany().HasForeignKey(entity => entity.MeetingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MeetingMinuteRevisionConfiguration : IEntityTypeConfiguration<MeetingMinuteRevision>
{
    public void Configure(EntityTypeBuilder<MeetingMinuteRevision> builder)
    {
        builder.ToTable("meeting_minute_revisions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Content).HasMaxLength(10000).IsRequired();
        builder.HasIndex(entity => new { entity.MeetingMinuteId, entity.CreatedAt });
        builder.HasOne<MeetingMinute>().WithMany().HasForeignKey(entity => entity.MeetingMinuteId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MeetingActionItemConfiguration : IEntityTypeConfiguration<MeetingActionItem>
{
    public void Configure(EntityTypeBuilder<MeetingActionItem> builder)
    {
        builder.ToTable("meeting_action_items");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Description).HasMaxLength(2000).IsRequired();
        builder.Property(entity => entity.Version).IsConcurrencyToken();
        builder.HasIndex(entity => new { entity.MeetingId, entity.IsCompleted });
        builder.HasIndex(entity => entity.ProjectTaskId).IsUnique();
        builder.HasOne<ProjectMeeting>().WithMany().HasForeignKey(entity => entity.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DNTU.SkillBridge.Domain.Workspaces.ProjectTask>().WithMany().HasForeignKey(entity => entity.ProjectTaskId).OnDelete(DeleteBehavior.Restrict);
    }
}
