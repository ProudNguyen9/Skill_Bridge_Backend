using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Application.Integrations;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Infrastructure.Integrations;

/// <summary>
/// Google Calendar based Meet-room creation. Uses the stored refresh token to mint short-lived
/// access tokens and inserts Calendar events with conferenceDataVersion=1 — Google returns a real
/// Meet room (https://meet.google.com/xxx-yyyy-zzz) attached to the event.
/// </summary>
public sealed class GoogleCalendarService(
    HttpClient httpClient,
    AppDbContext dbContext,
    IOptions<GoogleCalendarOptions> calendarOptions,
    IOptions<JwtOptions> jwtOptions,
    ILogger<GoogleCalendarService> logger) : IGoogleMeetLinkService
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string CalendarEventsEndpoint = "https://www.googleapis.com/calendar/v3/calendars/primary/events";
    private const string CalendarEventsScope = "https://www.googleapis.com/auth/calendar.events openid email";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private readonly GoogleCalendarOptions _options = calendarOptions.Value;
    private readonly string _stateKey = !string.IsNullOrWhiteSpace(calendarOptions.Value.StateSigningKey)
        ? calendarOptions.Value.StateSigningKey
        : jwtOptions.Value.SigningKey;

    public string BuildAuthorizationUrl(Guid userId, string? redirectUriOverride = null)
    {
        if (!_options.IsConfigured) throw new InvalidOperationException("GoogleCalendar integration is not configured.");
        var redirectUri = ResolveRedirectUri(redirectUriOverride);
        var state = SignState(userId);
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId!,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = CalendarEventsScope,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state
        };
        return AuthEndpoint + "?" + string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<bool> CompleteAuthorizationAsync(Guid userId, string code, string? redirectUriOverride = null, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured) return false;
        var redirectUri = ResolveRedirectUri(redirectUriOverride);

        var token = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId!,
            ["client_secret"] = _options.ClientSecret!,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        }, cancellationToken);
        if (token is null || string.IsNullOrWhiteSpace(token.RefreshToken)) return false;
        // A refresh token without the Calendar scope would fail on every later Meet-link creation
        // (ACCESS_TOKEN_SCOPE_INSUFFICIENT) — reject the connection up front instead.
        if (!string.IsNullOrWhiteSpace(token.Scope) && !token.Scope.Contains("auth/calendar.events", StringComparison.Ordinal))
        {
            logger.LogWarning("Google consent granted scope '{Scope}' without calendar.events — ask the user to re-connect after adding the scope to the OAuth consent screen.", token.Scope);
            return false;
        }

        var email = DecodeJwtEmail(token.IdToken) ?? "google-user";
        var existing = await dbContext.GoogleCalendarIntegrations.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (existing is null)
        {
            dbContext.GoogleCalendarIntegrations.Add(new Domain.Integrations.GoogleCalendarIntegration(userId, email, token.RefreshToken));
        }
        else
        {
            existing.UpdateTokens(email, token.RefreshToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(bool Connected, string? Email)> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var integration = await dbContext.GoogleCalendarIntegrations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return (integration is not null, integration?.ConnectedEmail);
    }

    public async Task DisconnectAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var integration = await dbContext.GoogleCalendarIntegrations.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (integration is null) return;
        dbContext.GoogleCalendarIntegrations.Remove(integration);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(GoogleLinkOutcome Outcome, GoogleMeetLinkResponse? Link)> CreateMeetLinkAsync(Guid userId, GoogleMeetLinkRequest request, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured) return (GoogleLinkOutcome.NotConfigured, null);
        var integration = await dbContext.GoogleCalendarIntegrations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (integration is null) return (GoogleLinkOutcome.NotConnected, null);

        var accessToken = await RefreshAccessTokenAsync(integration.RefreshToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken)) return (GoogleLinkOutcome.Failed, null);

        var payload = new
        {
            summary = request.Title,
            description = request.Description,
            start = new { dateTime = request.StartAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") },
            end = new { dateTime = request.EndAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") },
            conferenceData = new
            {
                createRequest = new
                {
                    requestId = $"skillbridge-{Guid.NewGuid():N}",
                    conferenceSolutionKey = new { type = "hangoutsMeet" }
                }
            }
        };

        using var httpResponse = new HttpRequestMessage(HttpMethod.Post, CalendarEventsEndpoint + "?conferenceDataVersion=1")
        {
            Content = JsonContent.Create(payload)
        };
        httpResponse.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(httpResponse, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Google Calendar event creation failed for user {UserId}: HTTP {Status} {Body}", userId, (int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            return (GoogleLinkOutcome.Failed, null);
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var meetUrl = json.TryGetProperty("hangoutLink", out var hangoutLink) && hangoutLink.ValueKind == JsonValueKind.String
            ? hangoutLink.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(meetUrl) && json.TryGetProperty("conferenceData", out var conference) && conference.TryGetProperty("entryPoints", out var entryPoints))
        {
            meetUrl = entryPoints.EnumerateArray()
                .Where(ep => ep.TryGetProperty("entryPointType", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "video")
                .Select(ep => ep.TryGetProperty("uri", out var uri) ? uri.GetString() : null)
                .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        }
        var eventId = json.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String ? idProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(meetUrl)) return (GoogleLinkOutcome.Failed, null);

        return (GoogleLinkOutcome.Success, new GoogleMeetLinkResponse(meetUrl!, eventId ?? string.Empty));
    }

    // ---- OAuth state signing (anti-CSRF / anti-replay) ----

    public static bool TryReadState(string state, string signingKey, out Guid userId)
    {
        userId = Guid.Empty;
        var parts = state.Split('.', 2);
        if (parts.Length != 2) return false;

        string payload;
        try
        {
            payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedSignature = ComputeSignature(payload, signingKey);
        byte[] providedSignature;
        try
        {
            providedSignature = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }
        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature)) return false;

        var segments = payload.Split('|');
        if (segments.Length != 3 || !Guid.TryParseExact(segments[0], "N", out userId)) return false;
        if (!long.TryParse(segments[1], out var expiryUnix) || DateTimeOffset.FromUnixTimeSeconds(expiryUnix) < DateTimeOffset.UtcNow) return false;
        return true;
    }

    private string SignState(Guid userId)
    {
        var payload = $"{userId:N}|{DateTimeOffset.UtcNow.Add(StateLifetime).ToUnixTimeSeconds()}|{Guid.NewGuid():N}";
        var payloadBase64Url = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signatureBase64Url = Base64UrlEncode(ComputeSignature(payload, _stateKey));
        return $"{payloadBase64Url}.{signatureBase64Url}";
    }

    private static byte[] ComputeSignature(string payload, string key) =>
        new HMACSHA256(Encoding.UTF8.GetBytes(key)).ComputeHash(Encoding.UTF8.GetBytes(payload));

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String((base64.Length % 4) switch { 2 => base64 + "==", 3 => base64 + "=", _ => base64 });
    }

    private string? ResolveRedirectUri(string? overrideValue) =>
        overrideValue ?? _options.RedirectUri ?? throw new InvalidOperationException("GoogleCalendar redirect URI is not configured.");

    private async Task<string?> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var token = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId!,
            ["client_secret"] = _options.ClientSecret!,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        }, cancellationToken);
        return token?.AccessToken;
    }

    private async Task<GoogleTokenResponse?> RequestTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Google token request failed: HTTP {Status} {Body}", (int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            return null;
        }
        return await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken);
    }

    private static string? DecodeJwtEmail(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;
        var parts = idToken.Split('.');
        if (parts.Length < 2) return null;
        try
        {
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var document = JsonDocument.Parse(payloadJson);
            return document.RootElement.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String ? email.GetString() : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private sealed record GoogleTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("id_token")] string? IdToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("scope")] string? Scope);
}
