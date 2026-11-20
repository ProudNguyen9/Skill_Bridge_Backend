using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Api.Authentication;

/// <summary>Sends meeting reminder emails through the Resend Email API (no-op when Email:Enabled is false).</summary>
public sealed class ResendMeetingEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions) : IMeetingReminderEmailSender
{
    private const string ResendEmailsEndpoint = "https://api.resend.com/emails";
    private readonly EmailOptions _email = emailOptions.Value;

    public async Task SendMeetingReminderAsync(IReadOnlyCollection<string> recipientEmails, string meetingTitle, DateTimeOffset startAtUtc, string? meetingUrl, CancellationToken cancellationToken)
    {
        if (!_email.Enabled || recipientEmails.Count == 0)
        {
            return;
        }

        var fromName = string.IsNullOrWhiteSpace(_email.FromName) ? "DNTU SkillBridge" : _email.FromName.Trim();
        var utc = startAtUtc.ToUniversalTime();
        var safeTitle = WebUtility.HtmlEncode(meetingTitle);
        var safeFromName = WebUtility.HtmlEncode(fromName) ?? "DNTU SkillBridge";
        var safeTime = WebUtility.HtmlEncode($"{utc:HH:mm} ngày {utc:dd/MM/yyyy} (giờ hệ thống)");
        var joinSection = string.IsNullOrWhiteSpace(meetingUrl)
            ? string.Empty
            : $@"<tr><td style=""padding:8px 0;""><a href=""{WebUtility.HtmlEncode(meetingUrl)}"" style=""display:inline-block;background:#AB1F24;color:#ffffff;padding:10px 18px;border-radius:8px;text-decoration:none;font-weight:600;"">Vào phòng họp</a></td></tr>";

        var html = $@"<div style=""font-family:Segoe UI,Arial,sans-serif;background:#f4f5f7;padding:24px;"">
  <div style=""max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e4e4e7;"">
    <div style=""background:#1e293b;color:#ffffff;padding:20px 24px;""><span style=""font-size:16px;font-weight:700;"">{safeFromName}</span><div style=""margin-top:6px;font-size:13px;opacity:.8;"">Nhắc lịch cuộc họp online</div></div>
    <div style=""padding:24px;color:#27272a;font-size:14px;line-height:1.6;"">
      <p style=""margin:0 0 12px;"">Cuộc họp <strong>{safeTitle}</strong> sẽ bắt đầu sau khoảng 30 phút nữa.</p>
      <p style=""margin:0 0 8px;""><strong>Thời gian:</strong> {safeTime}</p>
      {joinSection}
      <p style=""margin:16px 0 0;color:#71717a;font-size:12px;"">Email tự động từ hệ thống DNTU SkillBridge — vui lòng không trả lời.</p>
    </div>
  </div>
</div>";

        var text = $"Nhắc lịch: cuộc họp \"{meetingTitle}\" bắt đầu lúc {utc:HH:mm} ngày {utc:dd/MM/yyyy} (giờ hệ thống).{(string.IsNullOrWhiteSpace(meetingUrl) ? string.Empty : $" Link tham gia: {meetingUrl}")}";

        var payload = new ResendMeetingEmailRequest(
            $"{fromName} <{_email.FromAddress}>",
            recipientEmails,
            $"Nhắc lịch họp: {meetingTitle}",
            html,
            text);

        using var request = new HttpRequestMessage(HttpMethod.Post, ResendEmailsEndpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _email.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Resend rejected the meeting reminder email with HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                null,
                response.StatusCode);
        }
    }

    private sealed record ResendMeetingEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] IReadOnlyCollection<string> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);
}
