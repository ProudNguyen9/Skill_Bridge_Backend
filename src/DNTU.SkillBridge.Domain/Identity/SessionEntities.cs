using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Identity;

public sealed class UserSession : AuditableEntity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid FamilyId { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public ICollection<RefreshToken> RefreshTokens { get; } = [];

    private UserSession() { }

    public UserSession(Guid userId, DateTimeOffset expiresAt)
    {
        UserId = userId;
        FamilyId = Guid.CreateVersion7();
        ExpiresAt = expiresAt;
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset revokedAt) => RevokedAt ??= revokedAt;
}

public sealed class RefreshToken : AuditableEntity
{
    public Guid SessionId { get; private set; }
    public UserSession Session { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public int Sequence { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    private RefreshToken() { }

    public RefreshToken(Guid sessionId, string tokenHash, int sequence, DateTimeOffset expiresAt)
    {
        SessionId = sessionId;
        TokenHash = tokenHash;
        Sequence = sequence;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && RevokedAt is null && !IsExpired(now);

    public void MarkUsed(Guid replacementTokenId, DateTimeOffset usedAt)
    {
        UsedAt = usedAt;
        ReplacedByTokenId = replacementTokenId;
    }

    public void Revoke(DateTimeOffset revokedAt) => RevokedAt ??= revokedAt;
}

public enum AccountTokenPurpose
{
    EmailVerification = 1,
    PasswordReset = 2
}

public sealed class AccountToken : AuditableEntity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public AccountTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    private AccountToken() { }

    public AccountToken(Guid userId, AccountTokenPurpose purpose, string tokenHash, DateTimeOffset expiresAt)
    {
        UserId = userId;
        Purpose = purpose;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;

    public void Consume(DateTimeOffset consumedAt) => ConsumedAt ??= consumedAt;
}
