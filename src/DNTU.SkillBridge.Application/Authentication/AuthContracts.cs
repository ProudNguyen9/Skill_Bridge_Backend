using System.ComponentModel.DataAnnotations;

namespace DNTU.SkillBridge.Application.Authentication;

public enum RegistrationAccountType
{
    Student,
    Company
}

public sealed class RegisterRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;

    [Required]
    public RegistrationAccountType AccountType { get; init; }
}

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed class RefreshRequest
{
    [Required, StringLength(512, MinimumLength = 32)]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed class TokenRequest
{
    [Required, StringLength(512, MinimumLength = 32)]
    public string Token { get; init; } = string.Empty;
}

public sealed class EmailRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required, StringLength(512, MinimumLength = 32)]
    public string Token { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required, StringLength(128)]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid SessionId);

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    bool EmailVerified,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    string? ProfileType,
    Guid? StudentId,
    Guid? CompanyId,
    Guid? LecturerId,
    bool ProfileCompleted);

public sealed record SessionResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsActive);
