using DNTU.SkillBridge.Domain.Chat;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> builder)
    {
        builder.ToTable("user_follows");
        builder.HasKey(entity => new { entity.FollowerUserId, entity.FolloweeUserId });

        builder.HasIndex(entity => entity.FolloweeUserId);

        builder.HasCheckConstraint("CK_user_follows_not_self", "[FollowerUserId] <> [FolloweeUserId]");

        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.FollowerUserId).OnDelete(DeleteBehavior.Cascade);
        // SQL Server khong cho phep hai cascade path tu users -> user_follows.
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.FolloweeUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SocialActivityConfiguration : IEntityTypeConfiguration<SocialActivity>
{
    public void Configure(EntityTypeBuilder<SocialActivity> builder)
    {
        builder.ToTable("social_activities");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ActivityType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Summary).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.LinkUrl).HasMaxLength(300).IsRequired();

        builder.HasIndex(entity => new { entity.ActorUserId, entity.CreatedAt });

        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.ActorUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.ToTable("user_blocks");
        builder.HasKey(entity => new { entity.BlockerUserId, entity.BlockedUserId });

        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.BlockerUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.BlockedUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ChatReportConfiguration : IEntityTypeConfiguration<ChatReport>
{
    public void Configure(EntityTypeBuilder<ChatReport> builder)
    {
        builder.ToTable("chat_reports");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.ResolutionNote).HasMaxLength(1000);

        builder.HasIndex(entity => new { entity.Status, entity.CreatedAt });

        builder.HasOne(entity => entity.Message).WithMany().HasForeignKey(entity => entity.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.ReporterUser).WithMany().HasForeignKey(entity => entity.ReporterUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.ResolvedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
