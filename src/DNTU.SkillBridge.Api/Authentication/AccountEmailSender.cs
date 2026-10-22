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
              font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif;
              -webkit-font-smoothing: antialiased;
              -moz-osx-font-smoothing: grayscale;
              -webkit-text-size-adjust: 100%;
              overflow-x: hidden;
            }
            body, table, td, div, p, a, span {
              font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif;
            }
            .font-script {
              font-family: 'Dancing Script', 'Segoe Script', 'Brush Script MT', cursive !important;
            }
            @media only screen and (max-width: 640px) {
              .mail-outer { padding: 0 !important; }
              .mail-shell { width: 100% !important; max-width: 100% !important; }
              .header-pad { padding: 20px 16px 12px !important; }
              .card-wrap { padding: 0 12px !important; }
              .card-pad { padding: 24px 18px 28px !important; border-radius: 16px !important; }
              .greeting-title { font-size: 20px !important; }
              .intro-copy { font-size: 13px !important; line-height: 21px !important; }
              .otp-card { padding: 20px 10px 18px !important; }
              .otp-slot-td { padding: 0 2px !important; }
              .otp-slot { width: 38px !important; height: 50px !important; line-height: 50px !important; font-size: 24px !important; border-radius: 8px !important; }
              .otp-dash-td { padding: 0 3px !important; }
              .info-box-table td { display: table-cell !important; }
              .info-box-icon-cell { width: 48px !important; padding: 10px 8px 10px 10px !important; }
              .info-box-icon { width: 34px !important; height: 34px !important; line-height: 34px !important; }
              .info-box-text { padding: 10px 12px !important; font-size: 12px !important; line-height: 18px !important; }
              .footer-pad { padding: 0 16px 20px !important; }
              .footer-table td { display: block !important; width: 100% !important; }
              .footer-divider-cell { display: none !important; }
              .footer-right-cell { padding-top: 14px !important; padding-left: 0 !important; }
              .wave-copyright { font-size: 9.5px !important; }
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
                  
                  <!-- TOP HEADER SECTION -->
                  <tr>
                    <td class="header-pad" style="position: relative; padding: 28px 34px 14px; background-color: #f4f6fa;">
                      <!-- Decorative Top-Right Ribbon Bands -->
                      <div aria-hidden="true" style="position: absolute; top: 0; right: 0; width: 150px; height: 95px; overflow: hidden; pointer-events: none;">
                        <svg width="150" height="95" viewBox="0 0 150 95" fill="none" style="display: block;">
                          <polygon points="70,0 98,0 52,90 24,90" fill="#fde5e8" opacity="0.9"/>
                          <polygon points="108,0 142,0 90,95 56,95" fill="#fad1d6" opacity="0.85"/>
                          <polygon points="146,0 150,0 150,15 122,80 106,80" fill="#f8bcc3" opacity="0.6"/>
                        </svg>
                      </div>

                      <!-- Header Content Table -->
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="position: relative; z-index: 2;">
                        <tr>
                          <!-- Left: DNTU Logo + School Info -->
                          <td valign="middle" align="left">
                            <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                              <tr>
                                <td valign="middle" style="padding-right: 12px;">
                                  <img src="https://dntu.edu.vn/storage/Setting/01KG9CYF13JZHBT05Z66XVDNWW.png" width="30" height="70" alt="DNTU Logo" style="display: block; width: 30px; height: auto; max-height: 56px; border: 0;">
                                </td>
                                <td valign="middle">
                                  <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 13.5px; line-height: 17.5px; font-weight: 700; color: #1b365d; letter-spacing: 0.3px; text-transform: uppercase;">
                                    ĐẠI HỌC CÔNG NGHỆ<br>ĐỒNG NAI
                                  </div>
                                  <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; padding-top: 3px; font-size: 11px; line-height: 15px; font-weight: 400; color: #5c728e; letter-spacing: 0.2px;">
                                    Tri thức &nbsp;•&nbsp; Kỹ năng &nbsp;•&nbsp; Tương lai
                                  </div>
                                </td>
                              </tr>
                            </table>
                          </td>

                          <!-- Right: Brand Info -->
                          <td valign="middle" align="right" style="padding-right: 28px;">
                            <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 14.5px; line-height: 19px; font-weight: 700; color: #1b365d; letter-spacing: 0.2px;">
                              {{safeFromName}}
                            </div>
                            <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; padding-top: 3px; font-size: 11px; line-height: 15px; font-weight: 400; color: #5c728e; letter-spacing: 0.2px;">
                              Kết nối tri thức &amp; thực tiễn
                            </div>
                          </td>
                        </tr>
                      </table>

                      <!-- Red Accent Divider Line -->
                      <div style="margin-top: 15px; height: 2.5px; background-color: #bd1c28; border-radius: 2px; font-size: 0; line-height: 0;">&nbsp;</div>
                    </td>
                  </tr>

                  <!-- GAP -->
                  <tr><td height="18" style="height: 18px; font-size: 0; line-height: 0;">&nbsp;</td></tr>

                  <!-- MAIN FLOATING WHITE CARD -->
                  <tr>
                    <td class="card-wrap" style="padding: 0 32px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#ffffff" style="background-color: #ffffff; border-radius: 20px; box-shadow: 0 10px 32px rgba(25, 45, 70, 0.04); overflow: hidden;">
                        <tr>
                          <td class="card-pad" style="padding: 38px 36px 44px;">
                            <h1 class="greeting-title" style="margin: 0 0 14px; font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 22px; line-height: 28px; font-weight: 700; color: #1b365d;">
                              Chào bạn,
                            </h1>
                            <p class="intro-copy" style="margin: 0 0 24px; font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 14.5px; line-height: 23px; color: #384e68;">
                              Bạn vừa yêu cầu tạo tài khoản {{safeFromName}}. Nhập mã xác minh bên dưới để hoàn tất đăng ký:
                            </p>

                            <!-- OTP Verification Card (Clean White Card with DNTU Red Border) -->
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin: 28px 0 26px;">
                              <tr>
                                <td align="center" style="padding: 26px 24px; background-color: #ffffff; border: 1.5px solid #bd1c28; border-radius: 14px; box-shadow: 0 2px 8px rgba(189, 28, 40, 0.05);">
                                  <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 12px; font-weight: 700; color: #5c728e; letter-spacing: 1.2px; text-transform: uppercase; margin-bottom: 8px;">
                                    MÃ XÁC THỰC CỦA BẠN (OTP)
                                  </div>
                                  <div style="font-family: 'SF Mono', Consolas, 'Roboto Mono', Menlo, monospace; font-size: 44px; line-height: 48px; font-weight: 800; letter-spacing: 10px; color: #bd1c28; margin: 8px 0 14px 10px; user-select: all;">
                                    {{safeOtp}}
                                  </div>
                                  <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 12.5px; line-height: 18px; color: #64748b;">
                                    Mã có hiệu lực trong <strong style="color: #1b365d; font-weight: 700;">10 phút</strong> &bull; Tuyệt đối không chia sẻ mã này cho bất kỳ ai
                                  </div>
                                </td>
                              </tr>
                            </table>

                            <!-- Support / Notice (Clean, Natural Typography - No Gray Box) -->
                            <p style="margin: 0 0 28px; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 13.5px; line-height: 22px; color: #526581;">
                              Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.<br>
                              Cần hỗ trợ kỹ thuật? Liên hệ ban quản trị tại <a href="mailto:support-skillbridge@dntu.edu.vn" style="color: #bd1c28; text-decoration: none; font-weight: 600; border-bottom: 1px dashed #bd1c28;">support-skillbridge@dntu.edu.vn</a>.
                            </p>

                            <!-- Sign-off -->
                            <div class="font-script" style="font-size: 26px; line-height: 32px; font-style: italic; font-weight: 700; color: #bd1c28; margin-bottom: 4px;">
                              Trân trọng,
                            </div>
                            <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 15px; line-height: 22px; font-weight: 700; color: #1b365d;">
                              Đội ngũ {{safeFromName}}
                            </div>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>

                  <!-- GAP -->
                  <tr><td height="28" style="height: 28px; font-size: 0; line-height: 0;">&nbsp;</td></tr>

                  <!-- FOOTER CONTACT SECTION -->
                  <tr>
                    <td class="footer-pad" style="padding: 0 36px 26px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" class="footer-table">
                        <tr>
                          <!-- Left: Logo + School Name + Slogan -->
                          <td width="50%" valign="middle" align="left">
                            <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                              <tr>
                                <td valign="middle" style="padding-right: 12px;">
                                  <img src="https://dntu.edu.vn/storage/Setting/01KG9CYF13JZHBT05Z66XVDNWW.png" width="30" height="70" alt="DNTU Logo" style="display: block; width: 30px; height: auto; max-height: 56px; border: 0;">
                                </td>
                                <td valign="middle">
                                  <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 13.5px; line-height: 17.5px; font-weight: 700; color: #1b365d; letter-spacing: 0.3px; text-transform: uppercase;">
                                    ĐẠI HỌC CÔNG NGHỆ<br>ĐỒNG NAI
                                  </div>
                                  <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; padding-top: 3px; font-size: 11px; line-height: 15px; font-weight: 400; color: #5c728e;">
                                    Kiến tạo giá trị – Phát triển tương lai
                                  </div>
                                </td>
                              </tr>
                            </table>
                          </td>

                          <!-- Middle Divider -->
                          <td class="footer-divider-cell" width="36" align="center" valign="middle" style="width: 36px;">
                            <div style="width: 1px; height: 50px; background-color: #cbd5e1; font-size: 0; line-height: 0;">&nbsp;</div>
                          </td>

                          <!-- Right: Contact Channels -->
                          <td class="footer-right-cell" width="46%" valign="middle" align="left" style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 11.5px; line-height: 19px; color: #384e68;">
                            <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                              <tr>
                                <td valign="middle" style="padding-bottom: 5px;">
                                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#5c728e" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="display: inline-block; vertical-align: middle; margin-right: 8px;">
                                    <circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/>
                                  </svg>
                                  <a href="https://www.dntu.edu.vn" target="_blank" style="color: #384e68; text-decoration: none;">www.dntu.edu.vn</a>
                                </td>
                              </tr>
                              <tr>
                                <td valign="middle" style="padding-bottom: 5px;">
                                  <svg width="14" height="14" viewBox="0 0 24 24" fill="#5c728e" style="display: inline-block; vertical-align: middle; margin-right: 8px;">
                                    <path d="M18 2h-3a5 5 0 0 0-5 5v3H7v4h3v8h4v-8h3l1-4h-4V7a1 1 0 0 1 1-1h3z"/>
                                  </svg>
                                  <a href="https://facebook.com/dntu.edu.vn" target="_blank" style="color: #384e68; text-decoration: none;">facebook.com/dntu.edu.vn</a>
                                </td>
                              </tr>
                              <tr>
                                <td valign="top">
                                  <svg width="14" height="14" viewBox="0 0 24 24" fill="#5c728e" style="display: inline-block; vertical-align: middle; margin-right: 8px; margin-top: 3px;">
                                    <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3" fill="#f4f6fa"/>
                                  </svg>
                                  <span style="display: inline-block; vertical-align: top; color: #384e68;">
                                    Số 206, Nguyễn Khuyến, P. Trảng Dài,<br>TP. Biên Hòa, Đồng Nai
                                  </span>
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>

                  <!-- BOTTOM RED CURVED WAVE SECTION -->
                  <tr>
                    <td style="padding: 0; line-height: 0; font-size: 0;">
                      <div style="position: relative; width: 100%; overflow: hidden; line-height: 0; font-size: 0;">
                        <svg viewBox="0 0 682 118" width="100%" height="118" preserveAspectRatio="none" style="display: block; width: 100%; height: 118px;">
                          <defs>
                            <!-- Rich main red gradient -->
                            <linearGradient id="mainRed" x1="0%" y1="20%" x2="100%" y2="80%">
                              <stop offset="0%" stop-color="#c91a27"/>
                              <stop offset="30%" stop-color="#ba1420"/>
                              <stop offset="70%" stop-color="#8f0c15"/>
                              <stop offset="100%" stop-color="#730811"/>
                            </linearGradient>

                            <!-- Dark lower-left facet -->
                            <linearGradient id="darkFacet" x1="0%" y1="0%" x2="80%" y2="100%">
                              <stop offset="0%" stop-color="#800b14"/>
                              <stop offset="50%" stop-color="#6d0710"/>
                              <stop offset="100%" stop-color="#550309"/>
                            </linearGradient>

                            <!-- Luminous bright scarlet shine above the diagonal crease -->
                            <linearGradient id="creaseShine" x1="0%" y1="50%" x2="100%" y2="50%">
                              <stop offset="0%" stop-color="#e22a39" stop-opacity="0.8"/>
                              <stop offset="45%" stop-color="#c91a27" stop-opacity="0.4"/>
                              <stop offset="100%" stop-color="#930d17" stop-opacity="0"/>
                            </linearGradient>
                          </defs>

                          <!-- Main flowing wave background -->
                          <path d="M 0,8 C 150,24 310,29 460,24 C 550,21 625,16 682,12 L 682,118 L 0,118 Z" fill="url(#mainRed)"/>

                          <!-- Dark diagonal wedge on bottom-left -->
                          <path d="M 0,16 C 110,47 220,83 325,118 L 0,118 Z" fill="url(#darkFacet)"/>

                          <!-- Luminous shine along and above the crease -->
                          <path d="M 0,8 C 110,41 220,77 325,118 C 220,73 110,33 0,8 Z" fill="url(#creaseShine)"/>

                          <!-- Copyright text on right -->
                          <text class="wave-copyright" x="644" y="75" text-anchor="end" fill="#ffffff" font-size="11" font-weight="400" font-family="'Be Vietnam Pro', 'Inter', Arial, sans-serif" opacity="0.95">
                            © 2026 Đại học Công nghệ Đồng Nai - {{safeFromName}}
                          </text>
                        </svg>
                      </div>
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
        $"DNTU SKILLBRIDGE — MÃ XÁC MINH EMAIL\n\nChào bạn,\n\nBạn vừa yêu cầu tạo tài khoản DNTU SkillBridge. Nhập mã xác minh bên dưới để hoàn tất đăng ký:\n\n{otp}\n\nMã xác minh có hiệu lực trong 10 phút và chỉ sử dụng được một lần.\n\nNếu bạn không thực hiện yêu cầu này, hãy bỏ qua email. Cần hỗ trợ? Liên hệ support-skillbridge@dntu.edu.vn.\n\nTrân trọng,\nĐội ngũ DNTU SkillBridge\n\n© 2026 Đại học Công nghệ Đồng Nai";

    private static string BuildPasswordResetHtml(string safeUrl, string safeHeading, string safeDescription, string safeFromName) => $$"""
        <!doctype html>
        <html lang="vi">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta name="x-apple-disable-message-reformatting">
          <meta name="color-scheme" content="light">
          <title>{{safeHeading}} - {{safeFromName}}</title>
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
              font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif;
              -webkit-font-smoothing: antialiased;
              -moz-osx-font-smoothing: grayscale;
              -webkit-text-size-adjust: 100%;
              overflow-x: hidden;
            }
            body, table, td, div, p, a, span {
              font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif;
            }
            .font-script {
              font-family: 'Dancing Script', 'Segoe Script', 'Brush Script MT', cursive !important;
            }
            @media only screen and (max-width: 640px) {
              .mail-outer { padding: 0 !important; }
              .mail-shell { width: 100% !important; max-width: 100% !important; }
              .header-pad { padding: 20px 16px 12px !important; }
              .card-wrap { padding: 0 12px !important; }
              .card-pad { padding: 24px 18px 28px !important; border-radius: 16px !important; }
              .greeting-title { font-size: 20px !important; }
              .intro-copy { font-size: 13px !important; line-height: 21px !important; }
              .footer-pad { padding: 0 16px 20px !important; }
              .footer-table td { display: block !important; width: 100% !important; }
              .footer-divider-cell { display: none !important; }
              .footer-right-cell { padding-top: 14px !important; padding-left: 0 !important; }
              .wave-copyright { font-size: 9.5px !important; }
            }
          </style>
        </head>
        <body style="margin: 0; padding: 0; background-color: #f4f6fa;">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#f4f6fa" style="width: 100%; background-color: #f4f6fa; margin: 0; padding: 0;">
            <tr>
              <td align="center" class="mail-outer" style="padding: 0 0 0 0;">
                <table role="presentation" width="682" cellspacing="0" cellpadding="0" border="0" class="mail-shell" style="width: 100%; max-width: 682px; margin: 0 auto; background-color: #f4f6fa; position: relative;">
                  <tr>
                    <td class="header-pad" style="position: relative; padding: 28px 34px 14px; background-color: #f4f6fa;">
                      <div aria-hidden="true" style="position: absolute; top: 0; right: 0; width: 150px; height: 95px; overflow: hidden; pointer-events: none;">
                        <svg width="150" height="95" viewBox="0 0 150 95" fill="none" style="display: block;">
                          <polygon points="70,0 98,0 52,90 24,90" fill="#fde5e8" opacity="0.9"/>
                          <polygon points="108,0 142,0 90,95 56,95" fill="#fad1d6" opacity="0.85"/>
                          <polygon points="146,0 150,0 150,15 122,80 106,80" fill="#f8bcc3" opacity="0.6"/>
                        </svg>
                      </div>
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="position: relative; z-index: 2;">
                        <tr>
                          <td valign="middle" align="left">
                            <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                              <tr>
                                <td valign="middle" style="padding-right: 12px;">
                                  <img src="https://dntu.edu.vn/storage/Setting/01KG9CYF13JZHBT05Z66XVDNWW.png" width="30" height="70" alt="DNTU Logo" style="display: block; width: 30px; height: auto; max-height: 56px; border: 0;">
                                </td>
                                <td valign="middle">
                                  <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 13.5px; line-height: 17.5px; font-weight: 700; color: #1b365d; letter-spacing: 0.3px; text-transform: uppercase;">
                                    ĐẠI HỌC CÔNG NGHỆ<br>ĐỒNG NAI
                                  </div>
                                  <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; padding-top: 3px; font-size: 11px; line-height: 15px; font-weight: 400; color: #5c728e;">
                                    Tri thức &nbsp;•&nbsp; Kỹ năng &nbsp;•&nbsp; Tương lai
                                  </div>
                                </td>
                              </tr>
                            </table>
                          </td>
                          <td valign="middle" align="right" style="padding-right: 28px;">
                            <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 14.5px; line-height: 19px; font-weight: 700; color: #1b365d; letter-spacing: 0.2px;">
                              {{safeFromName}}
                            </div>
                            <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; padding-top: 3px; font-size: 11px; line-height: 15px; font-weight: 400; color: #5c728e;">
                              Kết nối tri thức &amp; thực tiễn
                            </div>
                          </td>
                        </tr>
                      </table>
                      <div style="margin-top: 15px; height: 2.5px; background-color: #bd1c28; border-radius: 2px; font-size: 0; line-height: 0;">&nbsp;</div>
                    </td>
                  </tr>
                  <tr><td height="18" style="height: 18px; font-size: 0; line-height: 0;">&nbsp;</td></tr>
                  <tr>
                    <td class="card-wrap" style="padding: 0 32px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#ffffff" style="background-color: #ffffff; border-radius: 20px; box-shadow: 0 10px 32px rgba(25, 45, 70, 0.04); overflow: hidden;">
                        <tr>
                          <td class="card-pad" style="padding: 38px 36px 44px;">
                            <h1 class="greeting-title" style="margin: 0 0 14px; font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 22px; line-height: 28px; font-weight: 700; color: #1b365d;">
                              {{safeHeading}}
                            </h1>
                            <p class="intro-copy" style="margin: 0 0 24px; font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 14.5px; line-height: 23px; color: #384e68;">
                              {{safeDescription}}
                            </p>
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin: 28px 0 26px;">
                              <tr>
                                <td align="center" style="padding: 28px 24px; background-color: #ffffff; border: 1.5px solid #bd1c28; border-radius: 14px; box-shadow: 0 2px 8px rgba(189, 28, 40, 0.05);">
                                  <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 12px; font-weight: 700; color: #5c728e; letter-spacing: 1.2px; text-transform: uppercase; margin-bottom: 16px;">
                                    LIÊN KẾT XÁC THỰC ĐẶT LẠI MẬT KHẨU
                                  </div>
                                  <div>
                                    <a href="{{safeUrl}}" target="_blank" style="display: inline-block; background-color: #bd1c28; color: #ffffff; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 15px; font-weight: 700; text-decoration: none; padding: 13px 36px; border-radius: 10px; box-shadow: 0 4px 12px rgba(189, 28, 40, 0.25);">
                                      Đặt Lại Mật Khẩu Ngay
                                    </a>
                                  </div>
                                  <div style="margin-top: 18px; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 12.5px; line-height: 19px; color: #64748b;">
                                    Liên kết có hiệu lực trong <strong style="color: #1b365d; font-weight: 700;">15 phút</strong> &bull; Chỉ sử dụng 1 lần duy nhất.<br>
                                    Nếu bạn không yêu cầu, vui lòng bỏ qua thư này, mật khẩu hiện tại vẫn an toàn.
                                  </div>
                                </td>
                              </tr>
                            </table>
                            <p style="margin: 0 0 28px; font-family: 'Be Vietnam Pro', 'Inter', Arial, sans-serif; font-size: 13px; line-height: 22px; color: #526581;">
                              Nếu bạn cần hỗ trợ kỹ thuật, vui lòng liên hệ ban quản trị tại <a href="mailto:support-skillbridge@dntu.edu.vn" style="color: #bd1c28; text-decoration: none; font-weight: 600; border-bottom: 1px dashed #bd1c28;">support-skillbridge@dntu.edu.vn</a>.
                            </p>
                            <div class="font-script" style="font-size: 26px; line-height: 32px; font-style: italic; font-weight: 700; color: #bd1c28; margin-bottom: 4px;">
                              Trân trọng,
                            </div>
                            <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 15px; line-height: 22px; font-weight: 700; color: #1b365d;">
                              Đội ngũ {{safeFromName}}
                            </div>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                  <tr><td height="28" style="height: 28px; font-size: 0; line-height: 0;">&nbsp;</td></tr>
                  <tr>
                    <td class="footer-pad" style="padding: 0 36px 26px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" class="footer-table">
                        <tr>
                          <td width="50%" valign="middle" align="left">
                            <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                              <tr>
                                <td valign="middle" style="padding-right: 12px;">
                                  <img src="https://dntu.edu.vn/storage/Setting/01KG9CYF13JZHBT05Z66XVDNWW.png" width="30" height="70" alt="DNTU Logo" style="display: block; width: 30px; height: auto; max-height: 56px; border: 0;">
                                </td>
                                <td valign="middle">
                                  <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 13.5px; line-height: 17.5px; font-weight: 700; color: #1b365d; letter-spacing: 0.3px; text-transform: uppercase;">
                                    ĐẠI HỌC CÔNG NGHỆ<br>ĐỒNG NAI
                                  </div>
                                  <div style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; padding-top: 3px; font-size: 11px; line-height: 15px; font-weight: 400; color: #5c728e;">
                                    Kiến tạo giá trị – Phát triển tương lai
                                  </div>
                                </td>
                              </tr>
                            </table>
                          </td>
                          <td class="footer-divider-cell" width="36" align="center" valign="middle" style="width: 36px;">
                            <div style="width: 1px; height: 50px; background-color: #cbd5e1; font-size: 0; line-height: 0;">&nbsp;</div>
                          </td>
                          <td class="footer-right-cell" width="46%" valign="middle" align="left" style="font-family: 'Be Vietnam Pro', 'Inter', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 11.5px; line-height: 19px; color: #384e68;">
                            <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                              <tr>
                                <td valign="middle" style="padding-bottom: 5px;">
                                  <a href="https://www.dntu.edu.vn" target="_blank" style="color: #384e68; text-decoration: none;">www.dntu.edu.vn</a>
                                </td>
                              </tr>
                              <tr>
                                <td valign="middle" style="padding-bottom: 5px;">
                                  <a href="https://facebook.com/dntu.edu.vn" target="_blank" style="color: #384e68; text-decoration: none;">facebook.com/dntu.edu.vn</a>
                                </td>
                              </tr>
                              <tr>
                                <td valign="top">
                                  <span style="color: #384e68;">Số 206, Nguyễn Khuyến, P. Trảng Dài, TP. Biên Hòa, Đồng Nai</span>
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding: 0; line-height: 0; font-size: 0;">
                      <div style="position: relative; width: 100%; overflow: hidden; line-height: 0; font-size: 0;">
                        <svg viewBox="0 0 682 118" width="100%" height="118" preserveAspectRatio="none" style="display: block; width: 100%; height: 118px;">
                          <defs>
                            <linearGradient id="mainRed" x1="0%" y1="20%" x2="100%" y2="80%">
                              <stop offset="0%" stop-color="#c91a27"/>
                              <stop offset="30%" stop-color="#ba1420"/>
                              <stop offset="70%" stop-color="#8f0c15"/>
                              <stop offset="100%" stop-color="#730811"/>
                            </linearGradient>
                            <linearGradient id="darkFacet" x1="0%" y1="0%" x2="80%" y2="100%">
                              <stop offset="0%" stop-color="#800b14"/>
                              <stop offset="50%" stop-color="#6d0710"/>
                              <stop offset="100%" stop-color="#550309"/>
                            </linearGradient>
                            <linearGradient id="creaseShine" x1="0%" y1="50%" x2="100%" y2="50%">
                              <stop offset="0%" stop-color="#e22a39" stop-opacity="0.8"/>
                              <stop offset="45%" stop-color="#c91a27" stop-opacity="0.4"/>
                              <stop offset="100%" stop-color="#930d17" stop-opacity="0"/>
                            </linearGradient>
                          </defs>
                          <path d="M 0,8 C 150,24 310,29 460,24 C 550,21 625,16 682,12 L 682,118 L 0,118 Z" fill="url(#mainRed)"/>
                          <path d="M 0,16 C 110,47 220,83 325,118 L 0,118 Z" fill="url(#darkFacet)"/>
                          <path d="M 0,8 C 110,41 220,77 325,118 C 220,73 110,33 0,8 Z" fill="url(#creaseShine)"/>
                          <text class="wave-copyright" x="644" y="75" text-anchor="end" fill="#ffffff" font-size="11" font-weight="400" font-family="'Be Vietnam Pro', 'Inter', Arial, sans-serif" opacity="0.95">
                            © 2026 Đại học Công nghệ Đồng Nai - {{safeFromName}}
                          </text>
                        </svg>
                      </div>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    private static string RenderOtpSlots(string safeOtp)
    {
        var trimmed = safeOtp?.Trim() ?? string.Empty;
        if (trimmed.Length == 6)
        {
            var d = trimmed.ToCharArray();
            return $$"""
                <table role="presentation" cellspacing="0" cellpadding="0" border="0" align="center" style="margin: 0 auto;">
                  <tr>
                    <td class="otp-slot-td" style="padding: 0 4px;"><div class="otp-slot" style="width: 46px; height: 56px; line-height: 56px; text-align: center; font-family: 'SF Mono', Consolas, 'Roboto Mono', 'Be Vietnam Pro', monospace; font-size: 28px; font-weight: 700; color: #bd1c28; background-color: #ffffff; border: 1.5px solid #cbd5e1; border-radius: 10px; box-shadow: 0 2px 4px rgba(15, 23, 42, 0.05);">{{d[0]}}</div></td>
                    <td class="otp-slot-td" style="padding: 0 4px;"><div class="otp-slot" style="width: 46px; height: 56px; line-height: 56px; text-align: center; font-family: 'SF Mono', Consolas, 'Roboto Mono', 'Be Vietnam Pro', monospace; font-size: 28px; font-weight: 700; color: #bd1c28; background-color: #ffffff; border: 1.5px solid #cbd5e1; border-radius: 10px; box-shadow: 0 2px 4px rgba(15, 23, 42, 0.05);">{{d[1]}}</div></td>
                    <td class="otp-slot-td" style="padding: 0 4px;"><div class="otp-slot" style="width: 46px; height: 56px; line-height: 56px; text-align: center; font-family: 'SF Mono', Consolas, 'Roboto Mono', 'Be Vietnam Pro', monospace; font-size: 28px; font-weight: 700; color: #bd1c28; background-color: #ffffff; border: 1.5px solid #cbd5e1; border-radius: 10px; box-shadow: 0 2px 4px rgba(15, 23, 42, 0.05);">{{d[2]}}</div></td>
                    <td class="otp-dash-td" style="padding: 0 6px; text-align: center; vertical-align: middle;"><div style="width: 8px; height: 2px; background-color: #94a3b8; border-radius: 1px; font-size: 0; line-height: 0; margin: 0 auto;">&nbsp;</div></td>
                    <td class="otp-slot-td" style="padding: 0 4px;"><div class="otp-slot" style="width: 46px; height: 56px; line-height: 56px; text-align: center; font-family: 'SF Mono', Consolas, 'Roboto Mono', 'Be Vietnam Pro', monospace; font-size: 28px; font-weight: 700; color: #bd1c28; background-color: #ffffff; border: 1.5px solid #cbd5e1; border-radius: 10px; box-shadow: 0 2px 4px rgba(15, 23, 42, 0.05);">{{d[3]}}</div></td>
                    <td class="otp-slot-td" style="padding: 0 4px;"><div class="otp-slot" style="width: 46px; height: 56px; line-height: 56px; text-align: center; font-family: 'SF Mono', Consolas, 'Roboto Mono', 'Be Vietnam Pro', monospace; font-size: 28px; font-weight: 700; color: #bd1c28; background-color: #ffffff; border: 1.5px solid #cbd5e1; border-radius: 10px; box-shadow: 0 2px 4px rgba(15, 23, 42, 0.05);">{{d[4]}}</div></td>
                    <td class="otp-slot-td" style="padding: 0 4px;"><div class="otp-slot" style="width: 46px; height: 56px; line-height: 56px; text-align: center; font-family: 'SF Mono', Consolas, 'Roboto Mono', 'Be Vietnam Pro', monospace; font-size: 28px; font-weight: 700; color: #bd1c28; background-color: #ffffff; border: 1.5px solid #cbd5e1; border-radius: 10px; box-shadow: 0 2px 4px rgba(15, 23, 42, 0.05);">{{d[5]}}</div></td>
                  </tr>
                </table>
                """;
        }

        return $$"""
            <div style="font-family: 'SF Mono', Consolas, 'Roboto Mono', monospace; font-size: 28px; font-weight: 700; letter-spacing: 5px; color: #bd1c28; text-align: center; background: #ffffff; padding: 12px 24px; border: 1.5px solid #cbd5e1; border-radius: 10px; display: inline-block;">{{safeOtp}}</div>
            """;
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
            $"{_email.FromName} <{_email.FromAddress}>",
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

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);
}
