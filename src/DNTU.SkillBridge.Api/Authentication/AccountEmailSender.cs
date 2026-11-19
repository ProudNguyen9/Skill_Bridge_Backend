using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DNTU.SkillBridge.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Api.Authentication;

public interface IAccountEmailSender
{
    Task SendVerificationAsync(string recipientEmail, string rawToken, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(string recipientEmail, string rawToken, CancellationToken cancellationToken);

    Task SendWelcomeEmailAsync(string recipientEmail, string displayName, string roleName, CancellationToken cancellationToken);
}

/// <summary>Sends account emails through the Resend Email API.</summary>
public sealed class ResendAccountEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions,
    IOptions<AppOptions> appOptions) : IAccountEmailSender
{
    private const string ResendEmailsEndpoint = "https://api.resend.com/emails";
    private readonly EmailOptions _email = emailOptions.Value;
    private readonly AppOptions _app = appOptions.Value;

    public Task SendVerificationAsync(string recipientEmail, string otp, CancellationToken cancellationToken) =>
        SendVerificationCodeAsync(recipientEmail, otp, cancellationToken);

    public Task SendPasswordResetAsync(string recipientEmail, string rawToken, CancellationToken cancellationToken) =>
        SendAccountLinkAsync(
            recipientEmail,
            rawToken,
            "/reset-password",
            "Đặt lại mật khẩu DNTU SkillBridge",
            "Đặt lại mật khẩu",
            "Nhấn nút bên dưới để tiếp tục đặt lại mật khẩu cho tài khoản SkillBridge.",
            cancellationToken);

    public Task SendWelcomeEmailAsync(string recipientEmail, string displayName, string roleName, CancellationToken cancellationToken) =>
        SendWelcomeNotificationAsync(recipientEmail, displayName, roleName, cancellationToken);

    private async Task SendVerificationCodeAsync(
        string recipientEmail,
        string otp,
        CancellationToken cancellationToken)
    {
        if (!_email.Enabled)
        {
            return;
        }

        var safeOtp = WebUtility.HtmlEncode(otp);
        var fromName = string.IsNullOrWhiteSpace(_email.FromName) ? "DNTU SkillBridge" : _email.FromName.Trim();
        var safeFromName = WebUtility.HtmlEncode(fromName) ?? "DNTU SkillBridge";
        var payload = new ResendEmailRequest(
            $"{fromName} <{_email.FromAddress}>",
            [recipientEmail],
            "Mã OTP xác minh tài khoản DNTU SkillBridge",
            BuildVerificationHtml(safeOtp, safeFromName),
            BuildVerificationText(otp));

        using var request = new HttpRequestMessage(HttpMethod.Post, ResendEmailsEndpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _email.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Resend rejected the email request with HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                null,
                response.StatusCode);
        }
    }

    private async Task SendWelcomeNotificationAsync(
        string recipientEmail,
        string displayName,
        string roleName,
        CancellationToken cancellationToken)
    {
        if (!_email.Enabled)
        {
            return;
        }

        var safeDisplayName = WebUtility.HtmlEncode(displayName);
        var safeRoleName = WebUtility.HtmlEncode(roleName);
        var safeRecipientEmail = WebUtility.HtmlEncode(recipientEmail);
        var fromName = string.IsNullOrWhiteSpace(_email.FromName) ? "DNTU SkillBridge" : _email.FromName.Trim();
        var safeFromName = WebUtility.HtmlEncode(fromName) ?? "DNTU SkillBridge";
        var appUrl = string.IsNullOrWhiteSpace(_app.FrontendBaseUrl) ? "http://localhost:3000" : _app.FrontendBaseUrl.TrimEnd('/');
        var safeAppUrl = WebUtility.HtmlEncode(appUrl);

        var payload = new ResendEmailRequest(
            $"{fromName} <{_email.FromAddress}>",
            [recipientEmail],
            $"Chào mừng bạn đến với DNTU SkillBridge - Tài khoản {roleName}",
            BuildWelcomeHtml(safeDisplayName, safeRoleName, safeRecipientEmail, safeAppUrl, safeFromName),
            BuildWelcomeText(displayName, roleName, recipientEmail, appUrl));

        using var request = new HttpRequestMessage(HttpMethod.Post, ResendEmailsEndpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _email.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Resend rejected the email request with HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                null,
                response.StatusCode);
        }
    }

    private async Task SendAccountLinkAsync(
        string recipientEmail,
        string rawToken,
        string route,
        string subject,
        string heading,
        string description,
        CancellationToken cancellationToken)
    {
        if (!_email.Enabled)
        {
            return;
        }

        var actionUrl = $"{_app.FrontendBaseUrl.TrimEnd('/')}{route}?token={Uri.EscapeDataString(rawToken)}";
        var safeUrl = WebUtility.HtmlEncode(actionUrl);
        var safeHeading = WebUtility.HtmlEncode(heading);
        var safeDescription = WebUtility.HtmlEncode(description);
        var fromName = string.IsNullOrWhiteSpace(_email.FromName) ? "DNTU SkillBridge" : _email.FromName.Trim();
        var safeFromName = WebUtility.HtmlEncode(fromName) ?? "DNTU SkillBridge";
        var payload = new ResendEmailRequest(
            $"{fromName} <{_email.FromAddress}>",
            [recipientEmail],
            subject,
            BuildPasswordResetHtml(safeUrl, safeHeading, safeDescription, safeFromName),
            $"{description}\n\n{actionUrl}\n\nNếu bạn không yêu cầu thao tác này, hãy bỏ qua email.");

        using var request = new HttpRequestMessage(HttpMethod.Post, ResendEmailsEndpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _email.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Resend rejected the email request with HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                null,
                response.StatusCode);
        }
    }

    private static string BuildWelcomeHtml(string safeDisplayName, string safeRoleName, string safeRecipientEmail, string safeAppUrl, string safeFromName) => $$"""
        <!doctype html>
        <html lang="vi">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta name="color-scheme" content="light">
          <title>Chào mừng bạn đến với {{safeFromName}}</title>
          <link rel="preconnect" href="https://fonts.googleapis.com">
          <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
          <link href="https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:ital,wght@0,400;0,500;0,600;0,700;1,400;1,600&family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
          <style>
            html, body {
              margin: 0; padding: 0; background-color: #f4f6fa; color: #334155;
              font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif;
            }
          </style>
        </head>
        <body style="margin: 0; padding: 0; background-color: #f4f6fa;">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#f4f6fa" style="width: 100%; background-color: #f4f6fa;">
            <tr>
              <td align="center" style="padding: 24px 12px;">
                <table role="presentation" width="640" cellspacing="0" cellpadding="0" border="0" style="width: 100%; max-width: 640px; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.06);">
                  <tr>
                    <td style="background: linear-gradient(135deg, #c91a27 0%, #8f0c15 100%); padding: 32px 36px; text-align: center;">
                      <h1 style="color: #ffffff; font-size: 22px; margin: 0; font-weight: 700; letter-spacing: 0.5px;">DNTU SKILLBRIDGE</h1>
                      <p style="color: #fde5e8; font-size: 13px; margin: 8px 0 0; opacity: 0.95;">Cổng Hợp tác Doanh nghiệp & Đồ án Thực chiến</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding: 36px 36px 28px;">
                      <h2 style="font-size: 18px; color: #1e293b; margin: 0 0 16px; font-weight: 600;">Xin chào {{safeDisplayName}},</h2>
                      <p style="font-size: 14px; line-height: 24px; color: #475569; margin: 0 0 20px;">
                        Chúc mừng bạn đã tạo thành công tài khoản trên nền tảng <strong>DNTU SkillBridge</strong> với vai trò <strong style="color: #c91a27;">{{safeRoleName}}</strong> thông qua xác thực Google.
                      </p>
                      
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; margin: 0 0 24px;">
                        <tr>
                          <td style="padding: 16px 20px;">
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="font-size: 13px; color: #334155;">
                              <tr>
                                <td style="padding: 6px 0; font-weight: 600; width: 140px; color: #64748b;">Email đăng nhập:</td>
                                <td style="padding: 6px 0; font-weight: 600; color: #0f172a;">{{safeRecipientEmail}}</td>
                              </tr>
                              <tr>
                                <td style="padding: 6px 0; font-weight: 600; color: #64748b;">Vai trò tài khoản:</td>
                                <td style="padding: 6px 0; font-weight: 600; color: #c91a27;">{{safeRoleName}}</td>
                              </tr>
                              <tr>
                                <td style="padding: 6px 0; font-weight: 600; color: #64748b;">Trạng thái:</td>
                                <td style="padding: 6px 0; font-weight: 600; color: #16a34a;">Đã xác minh qua Google</td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                      </table>

                      <div style="text-align: center; margin: 28px 0;">
                        <a href="{{safeAppUrl}}" target="_blank" style="display: inline-block; background-color: #c91a27; color: #ffffff; text-decoration: none; font-size: 14px; font-weight: 600; padding: 12px 32px; border-radius: 8px; box-shadow: 0 2px 8px rgba(201, 26, 39, 0.25);">
                          Truy cập DNTU SkillBridge
                        </a>
                      </div>

                      <p style="font-size: 12px; line-height: 18px; color: #94a3b8; margin: 24px 0 0; text-align: center;">
                        Nếu bạn không thực hiện đăng ký này, vui lòng liên hệ ngay với chúng tôi qua <a href="mailto:support-skillbridge@dntu.edu.vn" style="color: #c91a27; text-decoration: none;">support-skillbridge@dntu.edu.vn</a>.
                      </p>
                    </td>
                  </tr>
                  <tr>
                    <td style="background-color: #0f172a; padding: 20px 36px; text-align: center; color: #94a3b8; font-size: 11px; line-height: 18px;">
                      <p style="margin: 0;">Trường Đại học Công nghệ Đồng Nai (DNTU)</p>
                      <p style="margin: 4px 0 0;">Số 206, Nguyễn Khuyến, KP. 5, P. Trảng Dài, TP. Biên Hòa, Tỉnh Đồng Nai</p>
                      <p style="margin: 4px 0 0;">© 2026 DNTU SkillBridge. All rights reserved.</p>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;


    private static string BuildVerificationHtml(string safeOtp, string safeFromName) => $$"""
<!doctype html>
<html lang="vi">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="x-apple-disable-message-reformatting">
  <meta name="color-scheme" content="light">
  <title>Mã xác minh {{safeFromName}}</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:ital,wght@0,400;0,500;0,600;0,700;1,400;1,600&family=Inter:wght@400;500;600;700&family=Dancing+Script:wght@600;700&display=swap" rel="stylesheet">
  <style>
    @import url('https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:ital,wght@0,400;0,500;0,600;0,700;1,400;1,600&family=Inter:wght@400;500;600;700&family=Dancing+Script:wght@600;700&display=swap');

    html, body {
      margin: 0;
      padding: 0;
      background-color: #f4f6fa;
      color: #334155;
      font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      -webkit-font-smoothing: antialiased;
      -moz-osx-font-smoothing: grayscale;
      -webkit-text-size-adjust: 100%;
      overflow-x: hidden;
    }
    body, table, td, div, p, a, span {
      font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
    }
    .font-script {
      font-family: 'Dancing Script', 'Segoe Script', 'Brush Script MT', cursive !important;
    }
    .mobile-br { display: none; }
    @media only screen and (max-width: 640px) {
      .mobile-br { display: inline !important; }
      .mail-outer { padding: 0 !important; }
      .mail-shell { width: 100% !important; max-width: 100% !important; }
      .header-pad { padding: 20px 16px 12px !important; }
      .card-wrap { padding: 0 12px !important; }
      .card-pad { padding: 24px 18px 28px !important; border-radius: 16px !important; }
      .greeting-title { font-size: 20px !important; }
      .intro-copy { font-size: 13px !important; line-height: 21px !important; }
      .footer-pad { padding: 0 14px 20px !important; }
      .footer-table td { display: table-cell !important; }
      .footer-divider-cell { display: table-cell !important; width: 16px !important; }
      .footer-left-cell { width: 46% !important; padding-right: 4px !important; }
      .footer-left-title { font-size: 11px !important; line-height: 14px !important; }
      .footer-left-slogan { font-size: 9px !important; line-height: 12px !important; white-space: normal !important; }
      .footer-right-cell { width: 50% !important; padding-top: 0 !important; padding-left: 4px !important; font-size: 9.5px !important; line-height: 14px !important; }
      .footer-right-cell td { font-size: 9.5px !important; line-height: 13.5px !important; }
      .wave-copyright { font-size: 9.5px !important; padding-top: 8px !important; }
      .wave-wedge { border-bottom-width: 24px !important; border-right-width: 130px !important; }
      .btn-stack { display: block !important; width: 100% !important; margin-bottom: 10px !important; }
      .table-responsive { display: block !important; width: 100% !important; overflow-x: auto !important; }
    }
  </style>
</head>
<body style="margin: 0; padding: 0; background-color: #f4f6fa;">
  <div style="display: none; max-height: 0; overflow: hidden; opacity: 0; color: transparent;">
    Mã xác minh DNTU SkillBridge: có hiệu lực trong 10 phút.
  </div>

  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#f4f6fa" style="width: 100%; background-color: #f4f6fa; margin: 0; padding: 0;">
    <tr>
      <td align="center" class="mail-outer" style="padding: 0 0 0 0;">
        <table role="presentation" width="682" cellspacing="0" cellpadding="0" border="0" class="mail-shell" style="width: 100%; max-width: 682px; margin: 0 auto; background-color: #f4f6fa; position: relative;">
          
          <!-- TOP HEADER SECTION: Text directly over pink ribbons -->
          <tr>
            <td class="header-pad" style="padding: 18px 18px 12px; background-color: #f4f6fa;">
              <!-- Header Content Table -->
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                <tr>
                  <!-- Left: DNTU Logo + School Info -->
                  <td valign="middle" align="left">
                    <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                      <tr>
                        <td valign="middle" style="padding-right: 10px;">
                          <img src="https://dntu.edu.vn/storage/Setting/01KG9CYF13JZHBT05Z66XVDNWW.png" width="26" height="60" alt="DNTU Logo" style="display: block; width: 26px; height: auto; max-height: 52px; border: 0;">
                        </td>
                        <td valign="middle">
                          <div style="font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; font-size: 12px; line-height: 15.5px; font-weight: 700; color: #1b365d; letter-spacing: 0.2px; text-transform: uppercase; white-space: nowrap;">
                            ĐẠI HỌC CÔNG NGHỆ<br>ĐỒNG NAI
                          </div>
                          <div style="font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; padding-top: 2px; font-size: 9.5px; line-height: 13px; font-weight: 400; color: #5c728e; letter-spacing: 0.2px; white-space: nowrap;">
                            Tri thức &bull; Kỹ năng &bull; Tương lai
                          </div>
                        </td>
                      </tr>
                    </table>
                  </td>

                  <!-- Right: Brand Info with 3 stripes UNDER text -->
                  <td valign="middle" align="right" style="position: relative; padding-right: 4px; padding-top: 4px; padding-bottom: 4px; background-image: -webkit-linear-gradient(117deg, transparent 0%, transparent 30%, #fde5e8 30%, #fde5e8 47%, transparent 47%, transparent 53%, #fad1d6 53%, #fad1d6 71%, transparent 71%, transparent 74%, #f8bcc3 74%, #f8bcc3 83%, transparent 83%, transparent 100%); background-image: linear-gradient(117deg, transparent 0%, transparent 30%, #fde5e8 30%, #fde5e8 47%, transparent 47%, transparent 53%, #fad1d6 53%, #fad1d6 71%, transparent 71%, transparent 74%, #f8bcc3 74%, #f8bcc3 83%, transparent 83%, transparent 100%); background-position: right center; background-size: 180px 60px; background-repeat: no-repeat;">
                    <!-- SVG Overlay for high-res crispness -->
                    <div aria-hidden="true" style="position: absolute; right: 0; top: 0; bottom: 0; width: 180px; height: 100%; overflow: hidden; pointer-events: none; z-index: 1;">
                      <svg width="180" height="60" viewBox="0 0 180 60" fill="none" xmlns="http://www.w3.org/2000/svg" style="display: block; width: 180px; height: 100%;" preserveAspectRatio="none">
                        <polygon points="85,0 117,0 60,60 28,60" fill="#fde5e8" opacity="0.85"/>
                        <polygon points="128,0 164,0 102,60 66,60" fill="#fad1d6" opacity="0.8"/>
                        <polygon points="172,0 180,0 180,12 140,60 120,60" fill="#f8bcc3" opacity="0.55"/>
                      </svg>
                    </div>

                    <!-- Text content directly over ribbons -->
                    <div style="position: relative; z-index: 2; padding: 2px 2px;">
                      <div style="font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; font-size: 13.5px; line-height: 17px; font-weight: 700; color: #1b365d; letter-spacing: 0.2px; white-space: nowrap;">
                        {{safeFromName}}
                      </div>
                      <div style="font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; padding-top: 2px; font-size: 9.5px; line-height: 13px; font-weight: 500; color: #475d79; letter-spacing: 0.2px; white-space: nowrap;">
                        Kết nối tri thức &amp; thực tiễn
                      </div>
                    </div>
                  </td>
                </tr>
              </table>

              <!-- Red Accent Divider Line -->
              <div style="margin-top: 13px; height: 2px; background-color: #bd1c28; border-radius: 2px; font-size: 0; line-height: 0;">&nbsp;</div>
            </td>
          </tr>

          <!-- GAP -->
          <tr><td height="14" style="height: 14px; font-size: 0; line-height: 0;">&nbsp;</td></tr>

          <!-- MAIN FLOATING WHITE CARD -->
          <tr>
            <td class="card-wrap" style="padding: 0 16px;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#ffffff" style="background-color: #ffffff; border-radius: 16px; box-shadow: 0 8px 24px rgba(25, 45, 70, 0.04); overflow: hidden;">
                <tr>
                  <td class="card-pad" style="padding: 30px 20px 32px;">
                    
      <h1 class="greeting-title" style="margin: 0 0 14px; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 22px; line-height: 28px; font-weight: 700; color: #1b365d;">
        Chào bạn,
      </h1>
      <p class="intro-copy" style="margin: 0 0 24px; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 14px; line-height: 22px; color: #384e68;">
        Bạn vừa yêu cầu tạo tài khoản DNTU SkillBridge. Nhập mã xác minh bên dưới để hoàn tất đăng ký:
      </p>

      <!-- OTP Verification Card (Red Border) -->
      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin: 24px 0 22px;">
        <tr>
          <td align="center" style="padding: 22px 16px; background-color: #ffffff; border: 1.5px solid #bd1c28; border-radius: 12px; box-shadow: 0 2px 8px rgba(189, 28, 40, 0.05);">
            <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 11px; font-weight: 700; color: #5c728e; letter-spacing: 1.2px; text-transform: uppercase; margin-bottom: 8px;">
              MÃ XÁC THỰC CỦA BẠN (OTP)
            </div>
            <div style="font-family: 'SF Mono', Consolas, 'Roboto Mono', Menlo, monospace; font-size: 38px; line-height: 44px; font-weight: 800; letter-spacing: 8px; color: #bd1c28; margin: 8px 0 12px 8px; user-select: all;">
              {{safeOtp}}
            </div>
            <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 12px; line-height: 18px; color: #64748b;">
              Mã có hiệu lực trong <strong style="color: #1b365d; font-weight: 700;">10 phút</strong> &bull; Tuyệt đối không chia sẻ mã này cho bất kỳ ai
            </div>
          </td>
        </tr>
      </table>
    

                    <!-- Support / Notice (Clean, Natural Typography - No Gray Box) -->
                    <p style="margin: 0 0 24px; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 12.5px; line-height: 21px; color: #526581;">
                      Nếu bạn cần hỗ trợ kỹ thuật hoặc có bất kỳ câu hỏi nào, vui lòng liên hệ ban quản trị tại <a href="mailto:support-skillbridge@dntu.edu.vn" style="color: #bd1c28; text-decoration: none; font-weight: 600; border-bottom: 1px dashed #bd1c28;">support-skillbridge@dntu.edu.vn</a>.
                    </p>

                    <!-- Sign-off -->
                    <div class="font-script" style="font-size: 24px; line-height: 30px; font-style: italic; font-weight: 700; color: #bd1c28; margin-bottom: 3px;">
                      Trân trọng,
                    </div>
                    <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 14.5px; line-height: 21px; font-weight: 700; color: #1b365d;">
                      Đội ngũ DNTU SkillBridge
                    </div>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- GAP -->
          <tr><td height="22" style="height: 22px; font-size: 0; line-height: 0;">&nbsp;</td></tr>

          <!-- FOOTER CONTACT SECTION -->
          <tr>
            <td class="footer-pad" style="padding: 0 20px 20px;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" class="footer-table">
                <tr>
                  <!-- Left: School Info (Logo + Tên trường) -->
                  <td class="footer-left-cell" width="48%" valign="middle" align="left" style="padding-right: 12px;">
                    <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                      <tr>
                        <td valign="middle" style="padding-right: 10px;">
                          <img src="https://dntu.edu.vn/storage/Setting/01KG9CYF13JZHBT05Z66XVDNWW.png" width="28" height="60" alt="DNTU Logo" style="display: block; width: 28px; height: auto; max-height: 52px; border: 0;">
                        </td>
                        <td valign="middle">
                          <div class="footer-left-title" style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 13px; line-height: 17px; font-weight: 700; color: #1b365d; text-transform: uppercase;">
                            ĐẠI HỌC CÔNG NGHỆ<br>ĐỒNG NAI
                          </div>
                          <div class="footer-left-slogan" style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; padding-top: 3px; font-size: 11px; line-height: 15px; font-weight: 400; color: #5c728e; white-space: nowrap;">
                            Kiến tạo giá trị &ndash;<span class="mobile-br"><br></span> Phát triển tương lai
                          </div>
                        </td>
                      </tr>
                    </table>
                  </td>

                  <!-- Middle Divider (Vách ngăn dọc) -->
                  <td class="footer-divider-cell" width="28" align="center" valign="middle" style="width: 28px;">
                    <div style="width: 1px; height: 50px; background-color: #cbd5e1; font-size: 0; line-height: 0;">&nbsp;</div>
                  </td>

                  <!-- Right: Contact Channels (Bên vế phải logo) -->
                  <td class="footer-right-cell" width="48%" valign="middle" align="left" style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 11.5px; line-height: 17px; color: #384e68; padding-left: 6px;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                      <tr>
                        <td valign="middle" width="20" style="width: 20px; padding-bottom: 5px; font-size: 12px; line-height: 16px;">🌐</td>
                        <td valign="middle" style="padding-bottom: 5px; font-size: 11.5px; line-height: 16px;">
                          <a href="https://www.dntu.edu.vn" target="_blank" style="color: #384e68; text-decoration: none; font-weight: 500;">www.dntu.edu.vn</a>
                        </td>
                      </tr>
                      <tr>
                        <td valign="middle" width="20" style="width: 20px; padding-bottom: 5px; font-size: 12px; line-height: 16px;">📘</td>
                        <td valign="middle" style="padding-bottom: 5px; font-size: 11.5px; line-height: 16px;">
                          <a href="https://facebook.com/dntu.edu.vn" target="_blank" style="color: #384e68; text-decoration: none; font-weight: 500;">facebook.com/dntu.edu.vn</a>
                        </td>
                      </tr>
                      <tr>
                        <td valign="top" width="20" style="width: 20px; font-size: 12px; line-height: 16px; padding-top: 1px;">📍</td>
                        <td valign="top" style="font-size: 11px; line-height: 16px; color: #384e68;">
                          Số 206, Nguyễn Khuyến, P. Trảng Dài, TP. Biên Hòa, Đồng Nai
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

<!-- BOTTOM RED WAVE -->
<tr>
  <td style="background-color: #a3121d; padding: 0; line-height: 0; font-size: 0;">
    <div style="height: 22px; background-color: #f4f6fa; border-radius: 0 0 50% 50% / 0 0 22px 30px; font-size: 0; line-height: 0;">&nbsp;</div>
    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="width: 100%; border-collapse: collapse;">
      <tr>
        <td style="background-color: #a3121d; padding: 18px 16px 22px; text-align: center;">
          <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 11px; line-height: 17px; font-weight: 400; color: #ffffff; opacity: 0.95; text-align: center; letter-spacing: 0.2px;">
            &copy; 2026 Đại học Công nghệ Đồng Nai &bull; {{safeFromName}}
          </div>
        </td>
      </tr>
    </table>
  </td>
</tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>
""";


    private static string BuildPasswordResetHtml(string safeUrl, string safeHeading, string safeDescription, string safeFromName) => $$"""
<!doctype html>
<html lang="vi">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="x-apple-disable-message-reformatting">
  <meta name="color-scheme" content="light">
  <title>Yêu cầu đặt lại mật khẩu DNTU SkillBridge</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:ital,wght@0,400;0,500;0,600;0,700;1,400;1,600&family=Inter:wght@400;500;600;700&family=Dancing+Script:wght@600;700&display=swap" rel="stylesheet">
  <style>
    @import url('https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:ital,wght@0,400;0,500;0,600;0,700;1,400;1,600&family=Inter:wght@400;500;600;700&family=Dancing+Script:wght@600;700&display=swap');

    html, body {
      margin: 0;
      padding: 0;
      background-color: #f4f6fa;
      color: #334155;
      font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      -webkit-font-smoothing: antialiased;
      -moz-osx-font-smoothing: grayscale;
      -webkit-text-size-adjust: 100%;
      overflow-x: hidden;
    }
    body, table, td, div, p, a, span {
      font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
    }
    .font-script {
      font-family: 'Dancing Script', 'Segoe Script', 'Brush Script MT', cursive !important;
    }
    .mobile-br { display: none; }
    @media only screen and (max-width: 640px) {
      .mobile-br { display: inline !important; }
      .mail-outer { padding: 0 !important; }
      .mail-shell { width: 100% !important; max-width: 100% !important; }
      .header-pad { padding: 20px 16px 12px !important; }
      .card-wrap { padding: 0 12px !important; }
      .card-pad { padding: 24px 18px 28px !important; border-radius: 16px !important; }
      .greeting-title { font-size: 20px !important; }
      .intro-copy { font-size: 13px !important; line-height: 21px !important; }
      .footer-pad { padding: 0 14px 20px !important; }
      .footer-table td { display: table-cell !important; }
      .footer-divider-cell { display: table-cell !important; width: 16px !important; }
      .footer-left-cell { width: 46% !important; padding-right: 4px !important; }
      .footer-left-title { font-size: 11px !important; line-height: 14px !important; }
      .footer-left-slogan { font-size: 9px !important; line-height: 12px !important; white-space: normal !important; }
      .footer-right-cell { width: 50% !important; padding-top: 0 !important; padding-left: 4px !important; font-size: 9.5px !important; line-height: 14px !important; }
      .footer-right-cell td { font-size: 9.5px !important; line-height: 13.5px !important; }
      .wave-copyright { font-size: 9.5px !important; padding-top: 8px !important; }
      .wave-wedge { border-bottom-width: 24px !important; border-right-width: 130px !important; }
      .btn-stack { display: block !important; width: 100% !important; margin-bottom: 10px !important; }
      .table-responsive { display: block !important; width: 100% !important; overflow-x: auto !important; }
    }
  </style>
</head>
<body style="margin: 0; padding: 0; background-color: #f4f6fa;">
  <div style="display: none; max-height: 0; overflow: hidden; opacity: 0; color: transparent;">
    Yêu cầu khôi phục mật khẩu tài khoản DNTU SkillBridge có hiệu lực trong 15 phút.
  </div>

  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#f4f6fa" style="width: 100%; background-color: #f4f6fa; margin: 0; padding: 0;">
    <tr>
      <td align="center" class="mail-outer" style="padding: 0 0 0 0;">
        <table role="presentation" width="682" cellspacing="0" cellpadding="0" border="0" class="mail-shell" style="width: 100%; max-width: 682px; margin: 0 auto; background-color: #f4f6fa; position: relative;">
          <!-- TOP HEADER SECTION: Text directly over pink ribbons -->
          <tr>
            <td class="header-pad" style="padding: 18px 18px 12px; background-color: #f4f6fa;">
              <!-- Header Content Table -->
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                <tr>
                  <!-- Left: DNTU Logo + School Info -->
                  <td valign="middle" align="left">
                    <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                      <tr>
                        <td valign="middle" style="padding-right: 10px;">
                          <img src="https://dntu.edu.vn/storage/Setting/01KG9CYF13JZHBT05Z66XVDNWW.png" width="26" height="60" alt="DNTU Logo" style="display: block; width: 26px; height: auto; max-height: 52px; border: 0;">
                        </td>
                        <td valign="middle">
                          <div style="font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; font-size: 12px; line-height: 15.5px; font-weight: 700; color: #1b365d; letter-spacing: 0.2px; text-transform: uppercase; white-space: nowrap;">
                            ĐẠI HỌC CÔNG NGHỆ<br>ĐỒNG NAI
                          </div>
                          <div style="font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; padding-top: 2px; font-size: 9.5px; line-height: 13px; font-weight: 400; color: #5c728e; letter-spacing: 0.2px; white-space: nowrap;">
                            Tri thức &bull; Kỹ năng &bull; Tương lai
                          </div>
                        </td>
                      </tr>
                    </table>
                  </td>

                  <!-- Right: Brand Info with 3 stripes UNDER text -->
                  <td valign="middle" align="right" style="position: relative; padding-right: 4px; padding-top: 4px; padding-bottom: 4px; background-image: -webkit-linear-gradient(117deg, transparent 0%, transparent 30%, #fde5e8 30%, #fde5e8 47%, transparent 47%, transparent 53%, #fad1d6 53%, #fad1d6 71%, transparent 71%, transparent 74%, #f8bcc3 74%, #f8bcc3 83%, transparent 83%, transparent 100%); background-image: linear-gradient(117deg, transparent 0%, transparent 30%, #fde5e8 30%, #fde5e8 47%, transparent 47%, transparent 53%, #fad1d6 53%, #fad1d6 71%, transparent 71%, transparent 74%, #f8bcc3 74%, #f8bcc3 83%, transparent 83%, transparent 100%); background-position: right center; background-size: 180px 60px; background-repeat: no-repeat;">
                    <!-- SVG Overlay for high-res crispness -->
                    <div aria-hidden="true" style="position: absolute; right: 0; top: 0; bottom: 0; width: 180px; height: 100%; overflow: hidden; pointer-events: none; z-index: 1;">
                      <svg width="180" height="60" viewBox="0 0 180 60" fill="none" xmlns="http://www.w3.org/2000/svg" style="display: block; width: 180px; height: 100%;" preserveAspectRatio="none">
                        <polygon points="85,0 117,0 60,60 28,60" fill="#fde5e8" opacity="0.85"/>
                        <polygon points="128,0 164,0 102,60 66,60" fill="#fad1d6" opacity="0.8"/>
                        <polygon points="172,0 180,0 180,12 140,60 120,60" fill="#f8bcc3" opacity="0.55"/>
                      </svg>
                    </div>

                    <!-- Text content directly over ribbons -->
                    <div style="position: relative; z-index: 2; padding: 2px 2px;">
                      <div style="font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; font-size: 13.5px; line-height: 17px; font-weight: 700; color: #1b365d; letter-spacing: 0.2px; white-space: nowrap;">
                        {{safeFromName}}
                      </div>
                      <div style="font-family: 'Be Vietnam Pro', 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; padding-top: 2px; font-size: 9.5px; line-height: 13px; font-weight: 500; color: #475d79; letter-spacing: 0.2px; white-space: nowrap;">
                        Kết nối tri thức &amp; thực tiễn
                      </div>
                    </div>
                  </td>
                </tr>
              </table>

              <!-- Red Accent Divider Line -->
              <div style="margin-top: 13px; height: 2px; background-color: #bd1c28; border-radius: 2px; font-size: 0; line-height: 0;">&nbsp;</div>
            </td>
          </tr>

          <!-- GAP -->
          <tr><td height="14" style="height: 14px; font-size: 0; line-height: 0;">&nbsp;</td></tr>

          <!-- MAIN FLOATING WHITE CARD -->
          <tr>
            <td class="card-wrap" style="padding: 0 16px;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#ffffff" style="background-color: #ffffff; border-radius: 16px; box-shadow: 0 8px 24px rgba(25, 45, 70, 0.04); overflow: hidden;">
                <tr>
                  <td class="card-pad" style="padding: 30px 20px 32px;">
                    
      <h1 class="greeting-title" style="margin: 0 0 14px; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 22px; line-height: 28px; font-weight: 700; color: #1b365d;">
        Chào bạn,
      </h1>
      <p class="intro-copy" style="margin: 0 0 24px; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 14px; line-height: 22px; color: #384e68;">
        Hệ thống nhận được yêu cầu đặt lại mật khẩu cho tài khoản liên kết với địa chỉ email này. Nhấn nút bên dưới để tiến hành tạo mật khẩu mới:
      </p>

      <!-- Action Card (Red Border) -->
      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin: 24px 0 22px;">
        <tr>
          <td align="center" style="padding: 24px 18px; background-color: #ffffff; border: 1.5px solid #bd1c28; border-radius: 12px; box-shadow: 0 2px 8px rgba(189, 28, 40, 0.05);">
            <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 11px; font-weight: 700; color: #5c728e; letter-spacing: 1.2px; text-transform: uppercase; margin-bottom: 14px;">
              LIÊN KẾT XÁC THỰC ĐẶT LẠI MẬT KHẨU
            </div>
            <div>
              <a href="{{safeUrl}}" target="_blank" style="display: inline-block; background-color: #bd1c28; color: #ffffff; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 14.5px; font-weight: 700; text-decoration: none; padding: 12px 30px; border-radius: 8px; box-shadow: 0 4px 12px rgba(189, 28, 40, 0.25);">
                Đặt Lại Mật Khẩu Ngay
              </a>
            </div>
            <div style="margin-top: 16px; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 12px; line-height: 18px; color: #64748b;">
              Liên kết có hiệu lực trong <strong style="color: #1b365d; font-weight: 700;">15 phút</strong> &bull; Chỉ sử dụng 1 lần duy nhất.<br>
              Nếu bạn không yêu cầu, vui lòng bỏ qua thư này, mật khẩu hiện tại vẫn an toàn.
            </div>
          </td>
        </tr>
      </table>
    

                    <!-- Support / Notice (Clean, Natural Typography - No Gray Box) -->
                    <p style="margin: 0 0 24px; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 12.5px; line-height: 21px; color: #526581;">
                      Nếu bạn cần hỗ trợ kỹ thuật hoặc có bất kỳ câu hỏi nào, vui lòng liên hệ ban quản trị tại <a href="mailto:support-skillbridge@dntu.edu.vn" style="color: #bd1c28; text-decoration: none; font-weight: 600; border-bottom: 1px dashed #bd1c28;">support-skillbridge@dntu.edu.vn</a>.
                    </p>

                    <!-- Sign-off -->
                    <div class="font-script" style="font-size: 24px; line-height: 30px; font-style: italic; font-weight: 700; color: #bd1c28; margin-bottom: 3px;">
                      Trân trọng,
                    </div>
                    <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 14.5px; line-height: 21px; font-weight: 700; color: #1b365d;">
                      Đội ngũ DNTU SkillBridge
                    </div>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- GAP -->
          <tr><td height="22" style="height: 22px; font-size: 0; line-height: 0;">&nbsp;</td></tr>

          <!-- FOOTER CONTACT SECTION -->
          <tr>
            <td class="footer-pad" style="padding: 0 20px 20px;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" class="footer-table">
                <tr>
                  <!-- Left: School Info (Logo + Tên trường) -->
                  <td class="footer-left-cell" width="48%" valign="middle" align="left" style="padding-right: 12px;">
                    <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                      <tr>
                        <td valign="middle" style="padding-right: 10px;">
                          <img src="https://dntu.edu.vn/storage/Setting/01KG9CYF13JZHBT05Z66XVDNWW.png" width="28" height="60" alt="DNTU Logo" style="display: block; width: 28px; height: auto; max-height: 52px; border: 0;">
                        </td>
                        <td valign="middle">
                          <div class="footer-left-title" style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 13px; line-height: 17px; font-weight: 700; color: #1b365d; text-transform: uppercase;">
                            ĐẠI HỌC CÔNG NGHỆ<br>ĐỒNG NAI
                          </div>
                          <div class="footer-left-slogan" style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; padding-top: 3px; font-size: 11px; line-height: 15px; font-weight: 400; color: #5c728e; white-space: nowrap;">
                            Kiến tạo giá trị &ndash;<span class="mobile-br"><br></span> Phát triển tương lai
                          </div>
                        </td>
                      </tr>
                    </table>
                  </td>

                  <!-- Middle Divider (Vách ngăn dọc) -->
                  <td class="footer-divider-cell" width="28" align="center" valign="middle" style="width: 28px;">
                    <div style="width: 1px; height: 50px; background-color: #cbd5e1; font-size: 0; line-height: 0;">&nbsp;</div>
                  </td>

                  <!-- Right: Contact Channels (Bên vế phải logo) -->
                  <td class="footer-right-cell" width="48%" valign="middle" align="left" style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 11.5px; line-height: 17px; color: #384e68; padding-left: 6px;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                      <tr>
                        <td valign="middle" width="20" style="width: 20px; padding-bottom: 5px; font-size: 12px; line-height: 16px;">🌐</td>
                        <td valign="middle" style="padding-bottom: 5px; font-size: 11.5px; line-height: 16px;">
                          <a href="https://www.dntu.edu.vn" target="_blank" style="color: #384e68; text-decoration: none; font-weight: 500;">www.dntu.edu.vn</a>
                        </td>
                      </tr>
                      <tr>
                        <td valign="middle" width="20" style="width: 20px; padding-bottom: 5px; font-size: 12px; line-height: 16px;">📘</td>
                        <td valign="middle" style="padding-bottom: 5px; font-size: 11.5px; line-height: 16px;">
                          <a href="https://facebook.com/dntu.edu.vn" target="_blank" style="color: #384e68; text-decoration: none; font-weight: 500;">facebook.com/dntu.edu.vn</a>
                        </td>
                      </tr>
                      <tr>
                        <td valign="top" width="20" style="width: 20px; font-size: 12px; line-height: 16px; padding-top: 1px;">📍</td>
                        <td valign="top" style="font-size: 11px; line-height: 16px; color: #384e68;">
                          Số 206, Nguyễn Khuyến, P. Trảng Dài, TP. Biên Hòa, Đồng Nai
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

<!-- BOTTOM RED WAVE -->
<tr>
  <td style="background-color: #a3121d; padding: 0; line-height: 0; font-size: 0;">
    <div style="height: 22px; background-color: #f4f6fa; border-radius: 0 0 50% 50% / 0 0 22px 30px; font-size: 0; line-height: 0;">&nbsp;</div>
    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="width: 100%; border-collapse: collapse;">
      <tr>
        <td style="background-color: #a3121d; padding: 18px 16px 22px; text-align: center;">
          <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 11px; line-height: 17px; font-weight: 400; color: #ffffff; opacity: 0.95; text-align: center; letter-spacing: 0.2px;">
            &copy; 2026 Đại học Công nghệ Đồng Nai &bull; {{safeFromName}}
          </div>
        </td>
      </tr>
    </table>
  </td>
</tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>
""";

    private static string BuildVerificationText(string otp) =>
        $"DNTU SKILLBRIDGE — MÃ XÁC MINH EMAIL\n\nChào bạn,\n\nBạn vừa yêu cầu tạo tài khoản DNTU SkillBridge. Nhập mã xác minh bên dưới để hoàn tất đăng ký:\n\n{otp}\n\nMã xác minh có hiệu lực trong 10 phút và chỉ sử dụng được một lần.\n\nNếu bạn cần hỗ trợ kỹ thuật hoặc có bất kỳ câu hỏi nào, vui lòng liên hệ ban quản trị tại support-skillbridge@dntu.edu.vn.\n\nTrân trọng,\nĐội ngũ DNTU SkillBridge\n\n© 2026 Đại học Công nghệ Đồng Nai";

    private static string BuildWelcomeText(string displayName, string roleName, string recipientEmail, string appUrl) =>
        $"DNTU SKILLBRIDGE — CHÀO MỪNG BẠN GIA NHẬP\n\nXin chào {displayName},\n\nChúc mừng bạn đã tạo thành công tài khoản trên nền tảng DNTU SkillBridge với vai trò {roleName}.\n\nThông tin tài khoản:\n- Email: {recipientEmail}\n- Vai trò: {roleName}\n- Trạng thái: Đã xác thực qua Google\n\nTruy cập hệ thống tại: {appUrl}\n\nTrân trọng,\nĐội ngũ DNTU SkillBridge\n© 2026 Đại học Công nghệ Đồng Nai";

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);
}
