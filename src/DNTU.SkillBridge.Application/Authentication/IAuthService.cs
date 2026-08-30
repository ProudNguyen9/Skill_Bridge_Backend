using DNTU.SkillBridge.Domain.Identity;

namespace DNTU.SkillBridge.Application.Authentication;

public interface IAuthService
{
    Task<bool> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResponse?> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken);
    Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SessionResponse>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<CurrentUserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<string?> CreateAccountTokenAsync(string email, AccountTokenPurpose purpose, CancellationToken cancellationToken);
    Task<bool> VerifyEmailAsync(string rawToken, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(string rawToken, string newPassword, CancellationToken cancellationToken);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
}
