using DNTU.SkillBridge.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.UserId, entity.RevokedAt });
        builder.HasOne(entity => entity.User).WithMany().HasForeignKey(entity => entity.UserId);
        builder.HasMany(entity => entity.RefreshTokens).WithOne(entity => entity.Session).HasForeignKey(entity => entity.SessionId);
    }

    public sealed class AccountTokenConfiguration : IEntityTypeConfiguration<AccountToken>
    {
        public void Configure(EntityTypeBuilder<AccountToken> builder)
        {
            builder.ToTable("account_tokens");
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();
            builder.HasIndex(entity => entity.TokenHash).IsUnique();
            builder.HasIndex(entity => new { entity.UserId, entity.Purpose, entity.ExpiresAt });
            builder.HasOne(entity => entity.User).WithMany().HasForeignKey(entity => entity.UserId);
        }
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(entity => entity.TokenHash).IsUnique();
        builder.HasIndex(entity => new { entity.SessionId, entity.Sequence }).IsUnique();
    }
}
