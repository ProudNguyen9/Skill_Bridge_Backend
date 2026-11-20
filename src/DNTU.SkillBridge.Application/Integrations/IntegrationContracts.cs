using System.ComponentModel.DataAnnotations;

namespace DNTU.SkillBridge.Application.Integrations;

public enum GoogleLinkOutcome
{
    /// <summary>Meet link was created successfully.</summary>
    Success = 1,
    /// <summary>No Google account connected for this user.</summary>
    NotConnected = 2,
    /// <summary>GoogleCalendar credentials are not configured on the server.</summary>
    NotConfigured = 3,
    /// <summary>Google API rejected the request (expired grant, quota, etc.).</summary>
    Failed = 4
}

public sealed record GoogleCalendarStatusResponse(bool Connected, string? Email);

public sealed record GoogleAuthUrlResponse(string Url, string RedirectUri);

public sealed class GoogleMeetLinkRequest
{
    [Required, StringLength(300, MinimumLength = 2)] public string Title { get; init; } = string.Empty;
    [StringLength(1000)] public string? Description { get; init; }
    [Required] public DateTimeOffset StartAt { get; init; }
    [Required] public DateTimeOffset EndAt { get; init; }
}

public sealed record GoogleMeetLinkResponse(string MeetingUrl, string CalendarEventId);

/// <summary>Creates Google Meet rooms by inserting Calendar events with attached conference data for a connected user.</summary>
public interface IGoogleMeetLinkService
{
    /// <summary>Builds the Google consent URL; the state parameter is HMAC-signed against tampering/replay.</summary>
    string BuildAuthorizationUrl(Guid userId, string? redirectUriOverride = null);

    /// <summary>Exchanges the OAuth code for tokens and stores the integration. Returns false when Google rejects the grant.</summary>
    Task<bool> CompleteAuthorizationAsync(Guid userId, string code, string? redirectUriOverride = null, CancellationToken cancellationToken = default);

    Task<(bool Connected, string? Email)> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    Task DisconnectAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<(GoogleLinkOutcome Outcome, GoogleMeetLinkResponse? Link)> CreateMeetLinkAsync(Guid userId, GoogleMeetLinkRequest request, CancellationToken cancellationToken = default);
}
