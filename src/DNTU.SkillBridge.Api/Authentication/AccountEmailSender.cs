namespace DNTU.SkillBridge.Api.Authentication;

public interface IAccountEmailSender
{
    Task SendVerificationAsync(string recipientEmail, string rawToken, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(string recipientEmail, string rawToken, CancellationToken cancellationToken);
}

/// <summary>
/// Development-safe placeholder. It intentionally does not emit token-bearing messages to logs.
/// Task 37 replaces this with an outbox-backed sender.
/// </summary>
public sealed class NullAccountEmailSender : IAccountEmailSender
{
    public Task SendVerificationAsync(string recipientEmail, string rawToken, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendPasswordResetAsync(string recipientEmail, string rawToken, CancellationToken cancellationToken) => Task.CompletedTask;
}
