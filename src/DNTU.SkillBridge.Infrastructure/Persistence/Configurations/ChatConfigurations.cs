using DNTU.SkillBridge.Domain.Chat;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(200);
        builder.Property(entity => entity.AvatarUrl).HasMaxLength(500);
        builder.Property(entity => entity.DirectPairKey).HasMaxLength(80);
        builder.Property(entity => entity.LastMessagePreview).HasMaxLength(240);
        builder.Property(entity => entity.CreatedByUserId).IsRequired();

        // Một hội thoại Direct duy nhất cho mỗi cặp người dùng.
        builder.HasIndex(entity => entity.DirectPairKey).IsUnique().HasFilter("[DirectPairKey] IS NOT NULL");
        // Một phòng nhóm duy nhất cho mỗi dự án.
        builder.HasIndex(entity => entity.ProjectId).IsUnique().HasFilter("[ProjectId] IS NOT NULL");
        builder.HasIndex(entity => entity.LastMessageAt);

        builder.HasOne(entity => entity.Project).WithMany().HasForeignKey(entity => entity.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.LastMessage).WithMany().HasForeignKey(entity => entity.LastMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entity => entity.Messages).WithOne(entity => entity.Conversation).HasForeignKey(entity => entity.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ToTable("conversation_participants");
        builder.HasKey(entity => new { entity.ConversationId, entity.UserId });
        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.RequestStatus).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(entity => new { entity.UserId, entity.RequestStatus, entity.LeftAt });

        builder.HasOne(entity => entity.Conversation).WithMany(entity => entity.Participants).HasForeignKey(entity => entity.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.User).WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.MessageType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Content).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.MetadataJson);

        // Cursor pagination: (ConversationId, CreatedAt DESC, Id DESC).
        builder.HasIndex(entity => new { entity.ConversationId, entity.CreatedAt, entity.Id });

        builder.HasOne(entity => entity.Conversation).WithMany(entity => entity.Messages).HasForeignKey(entity => entity.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.SenderUser).WithMany().HasForeignKey(entity => entity.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ReplyTo).WithMany().HasForeignKey(entity => entity.ReplyToMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entity => entity.Reactions).WithOne(entity => entity.Message).HasForeignKey(entity => entity.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ChatMessageReactionConfiguration : IEntityTypeConfiguration<ChatMessageReaction>
{
    public void Configure(EntityTypeBuilder<ChatMessageReaction> builder)
    {
        builder.ToTable("chat_message_reactions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Emoji).HasMaxLength(16).IsRequired();

        builder.HasIndex(entity => new { entity.MessageId, entity.UserId, entity.Emoji }).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
