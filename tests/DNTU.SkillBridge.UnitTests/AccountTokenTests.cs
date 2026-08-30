using DNTU.SkillBridge.Domain.Identity;

namespace DNTU.SkillBridge.UnitTests;

public sealed class AccountTokenTests
{
    [Fact]
    public void IsUsable_returns_false_after_consumption_or_expiry()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new AccountToken(Guid.NewGuid(), AccountTokenPurpose.PasswordReset, "hash", now.AddMinutes(1));

        Assert.True(token.IsUsable(now));

        token.Consume(now);

        Assert.False(token.IsUsable(now));
        Assert.False(new AccountToken(Guid.NewGuid(), AccountTokenPurpose.EmailVerification, "hash2", now).IsUsable(now));
    }
}
