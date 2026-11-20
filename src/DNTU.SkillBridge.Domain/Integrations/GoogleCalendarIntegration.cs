using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Integrations;

/// <summary>
/// One user's connected Google account for Meet-link creation. The refresh token lets the
/// backend create Calendar events with attached Google Meet rooms on the user's behalf.
/// One integration per user (unique index on UserId).
/// </summary>
public sealed class GoogleCalendarIntegration : AuditableEntity
{
    public Guid UserId { get; private set; }
    public string ConnectedEmail { get; private set; } = string.Empty;
    public string RefreshToken { get; private set; } = string.Empty;

    private GoogleCalendarIntegration() { }

    public GoogleCalendarIntegration(Guid userId, string connectedEmail, string refreshToken)
    {
        UserId = userId;
        ConnectedEmail = connectedEmail;
        RefreshToken = refreshToken;
    }

    public void UpdateTokens(string connectedEmail, string refreshToken)
    {
        ConnectedEmail = connectedEmail;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            // Google occasionally rotates the refresh token on consent; keep the existing one otherwise.
            RefreshToken = refreshToken;
        }
    }
}
