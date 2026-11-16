using DNTU.SkillBridge.Domain.Identity;

namespace DNTU.SkillBridge.Application.Authentication;

public record RegisterResult(bool Succeeded, string? TargetField = null, string? ErrorMessage = null)
{
    public static RegisterResult Success() => new(true);
    public static RegisterResult Failed(string targetField, string errorMessage) => new(false, targetField, errorMessage);
}

public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResponse?> LoginWithGoogleAsync(GoogleIdentity identity, RegistrationAccountType? targetRole, CancellationToken cancellationToken);
    Task<AuthResponse?> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken);
    Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SessionResponse>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<CurrentUserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<string?> CreateAccountTokenAsync(string email, AccountTokenPurpose purpose, CancellationToken cancellationToken);
    Task<bool> VerifyEmailAsync(string email, string otp, CancellationToken cancellationToken);
    Task<AuthResponse?> VerifyEmailAndLoginAsync(string email, string otp, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(string rawToken, string newPassword, CancellationToken cancellationToken);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
}
