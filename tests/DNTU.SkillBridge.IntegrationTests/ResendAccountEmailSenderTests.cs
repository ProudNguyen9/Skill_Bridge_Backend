using System.Net;
using System.Text.Json;
using DNTU.SkillBridge.Api.Authentication;
using DNTU.SkillBridge.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.IntegrationTests;

public sealed class ResendAccountEmailSenderTests
{
    [Fact]
    public async Task SendsEmailVerificationOtpThroughResend()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var sender = CreateSender(client, enabled: true);

        await sender.SendVerificationAsync("student@dntu.edu.vn", "123456", CancellationToken.None);

        Assert.Equal("https://api.resend.com/emails", handler.RequestUri?.ToString());
        Assert.Equal("Bearer re_test_key", handler.Authorization);
        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var root = payload.RootElement;
        Assert.Equal("DNTU SkillBridge <no-reply@phelieuminhduc.com>", root.GetProperty("from").GetString());
        Assert.Equal("student@dntu.edu.vn", root.GetProperty("to")[0].GetString());
        Assert.Equal("Mã OTP xác minh tài khoản DNTU SkillBridge", root.GetProperty("subject").GetString());
        Assert.Contains("123456", root.GetProperty("html").GetString());
        Assert.Contains("10 phút", root.GetProperty("html").GetString());
        Assert.Contains("123456", root.GetProperty("text").GetString());
        Assert.DoesNotContain("/verify-email?token=", root.GetProperty("html").GetString());
    }

    [Fact]
    public async Task SendsPasswordResetLinkThroughResend()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var sender = CreateSender(client, enabled: true);

        await sender.SendPasswordResetAsync("student@dntu.edu.vn", "token+value", CancellationToken.None);

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var root = payload.RootElement;
        Assert.Equal("Đặt lại mật khẩu DNTU SkillBridge", root.GetProperty("subject").GetString());
        Assert.Contains("https://app.skillbridge.test/reset-password?token=token%2Bvalue", root.GetProperty("html").GetString());
        Assert.Contains("https://app.skillbridge.test/reset-password?token=token%2Bvalue", root.GetProperty("text").GetString());
    }

    [Fact]
    public async Task DisabledEmailDoesNotCallResend()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var sender = CreateSender(client, enabled: false);

        await sender.SendVerificationAsync("student@dntu.edu.vn", "token", CancellationToken.None);

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ResendFailureIsSurfacedWithoutIncludingTheToken()
    {
        var handler = new RecordingHandler(HttpStatusCode.Forbidden);
        using var client = new HttpClient(handler);
        var sender = CreateSender(client, enabled: true);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendPasswordResetAsync("student@dntu.edu.vn", "private-token", CancellationToken.None));

        Assert.Contains("HTTP 403", exception.Message);
        Assert.DoesNotContain("private-token", exception.Message);
    }

    private static ResendAccountEmailSender CreateSender(HttpClient client, bool enabled) => new(
        client,
        Options.Create(new EmailOptions
        {
            Enabled = enabled,
            ApiKey = "re_test_key",
            FromAddress = "no-reply@phelieuminhduc.com",
            FromName = "DNTU SkillBridge"
        }),
        Options.Create(new AppOptions { FrontendBaseUrl = "https://app.skillbridge.test" }));

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? Authorization { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }
}
