using Asp.Versioning;
using DNTU.SkillBridge.Api.Authentication;
using DNTU.SkillBridge.Application.Authentication;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Google.Apis.Auth;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(
    IAuthService authService,
    ICurrentUser currentUser,
    IAccountEmailSender accountEmailSender,
    IOptions<GoogleAuthOptions> googleAuthOptions) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(result.TargetField ?? "email", result.ErrorMessage ?? "Không thể hoàn tất đăng ký.");
            return ValidationProblem(ModelState);
        }

        var verificationOtp = await authService.CreateAccountTokenAsync(request.Email, DNTU.SkillBridge.Domain.Identity.AccountTokenPurpose.EmailVerification, cancellationToken);
        if (verificationOtp is not null)
        {
            await accountEmailSender.SendVerificationAsync(request.Email, verificationOtp, cancellationToken);
        }

        return Created("", new ApiResponse<object>(new { message = "Registration completed. Check your email for the verification code." }));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result is null ? Unauthorized() : Ok(new ApiResponse<AuthResponse>(result));
    }

    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> LoginWithGoogle(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var clientId = googleAuthOptions.Value.ClientId.Trim();

        string email;
        string subject;
        string? name;

        // Cho phép dev token định dạng dev-google-token:email:name:subject để kiểm thử môi trường local
        if (request.Credential.StartsWith("dev-google-token:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = request.Credential.Split(':');
            email = parts.Length > 1 ? parts[1].Trim() : "student@dntu.edu.vn";
            name = parts.Length > 2 ? parts[2].Trim() : email[..email.IndexOf('@')];
            subject = parts.Length > 3 ? parts[3].Trim() : "dev-sub-" + email.GetHashCode();
        }
        else
        {
            if (clientId.Length == 0)
            {
                return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Google sign-in is not configured.");
            }

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(
                    request.Credential,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = [clientId]
                        // Không đặt HostedDomain ở đây để cho phép tài khoản Google Doanh nghiệp (Gmail, công ty riêng)
                    });
            }
            catch (InvalidJwtException)
            {
                return Unauthorized();
            }

            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            {
                return Unauthorized();
            }

            email = payload.Email.Trim();
            subject = payload.Subject;
            name = payload.Name?.Trim();
        }

        var isDntu = IsDntuEmail(email);

        // Quy tắc: Sinh viên và Giảng viên BẮT BUỘC dùng email Google của DNTU (@dntu.edu.vn)
        if (request.TargetRole.HasValue)
        {
            if ((request.TargetRole == RegistrationAccountType.Student || request.TargetRole == RegistrationAccountType.Lecturer) && !isDntu)
            {
                return BadRequest(new ApiResponse<object>(new { message = "Sinh viên và Giảng viên bắt buộc sử dụng tài khoản Google DNTU (@dntu.edu.vn)." }));
            }
        }

        var displayName = string.IsNullOrWhiteSpace(name) ? email[..email.IndexOf('@')] : name;
        var result = await authService.LoginWithGoogleAsync(
            new GoogleIdentity(subject, email, displayName),
            request.TargetRole,
            cancellationToken);

        if (result is null)
        {
            return Unauthorized();
        }

        // Đăng nhập / Đăng ký qua Google không cần gửi OTP vì danh tính email đã được Google kiểm duyệt & xác thực
        return Ok(new ApiResponse<AuthResponse>(result));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return result is null ? Unauthorized() : Ok(new ApiResponse<AuthResponse>(result));
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.SessionId is not { } sessionId)
        {
            return Unauthorized();
        }

        await authService.RevokeSessionAsync(userId, sessionId, cancellationToken);
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        await authService.RevokeAllSessionsAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CurrentUserResponse>>> GetMe(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await authService.GetCurrentUserAsync(userId, cancellationToken);
        return result is null ? Unauthorized() : Ok(new ApiResponse<CurrentUserResponse>(result));
    }

    [HttpGet("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<SessionResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SessionResponse>>>> GetSessions(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(new ApiResponse<IReadOnlyCollection<SessionResponse>>(await authService.GetSessionsAsync(userId, cancellationToken)));
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        await authService.RevokeSessionAsync(userId, sessionId, cancellationToken);
        return NoContent();
    }
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> VerifyEmail(VerifyEmailOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.VerifyEmailAndLoginAsync(request.Email, request.Otp, cancellationToken);
        if (result is null)
        {
            return Unauthorized();
        }

        // Gửi email chào mừng bằng template DNTU sau khi xác minh email thành công
        try
        {
            var roleLabel = !string.IsNullOrWhiteSpace(result.RoleName) ? result.RoleName : "Thành viên";
            var displayName = !string.IsNullOrWhiteSpace(result.DisplayName) ? result.DisplayName : request.Email[..request.Email.IndexOf('@')];
            await accountEmailSender.SendWelcomeEmailAsync(request.Email, displayName, roleLabel, cancellationToken);
        }
        catch
        {
            // Không để lỗi gửi email làm gián đoạn
        }

        return Ok(new ApiResponse<AuthResponse>(result));
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendVerification(EmailRequest request, CancellationToken cancellationToken)
    {
        var otp = await authService.CreateAccountTokenAsync(request.Email, DNTU.SkillBridge.Domain.Identity.AccountTokenPurpose.EmailVerification, cancellationToken);
        if (otp is not null)
        {
            await accountEmailSender.SendVerificationAsync(request.Email, otp, cancellationToken);
        }

        return Accepted();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-recovery")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken cancellationToken)
    {
        var token = await authService.CreateAccountTokenAsync(request.Email, DNTU.SkillBridge.Domain.Identity.AccountTokenPurpose.PasswordReset, cancellationToken);
        if (token is not null)
        {
            await accountEmailSender.SendPasswordResetAsync(request.Email, token, cancellationToken);
        }

        return Accepted();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        await authService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken) ? NoContent() : Unauthorized();

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return await authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken) ? NoContent() : Unauthorized();
    }

    private static bool IsDntuEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var separator = email.LastIndexOf('@');
        return separator > 0 && email[(separator + 1)..].Equals(GoogleAuthOptions.DntuHostedDomain, StringComparison.OrdinalIgnoreCase);
    }
}
