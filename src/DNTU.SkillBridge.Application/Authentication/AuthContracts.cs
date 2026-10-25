using System.ComponentModel.DataAnnotations;

namespace DNTU.SkillBridge.Application.Authentication;

public enum RegistrationAccountType
{
    Student,
    Company,
    Lecturer
}

public sealed class RegisterRequest : IValidatableObject
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    [StringLength(32)]
    public string? StudentCode { get; init; }

    [StringLength(32)]
    public string? LecturerCode { get; init; }

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;

    [Required, EnumDataType(typeof(RegistrationAccountType))]
    public RegistrationAccountType AccountType { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AccountType == RegistrationAccountType.Student)
        {
            if (!IsCampusEmail())
            {
                yield return new ValidationResult(
                    "Sinh viên cần đăng ký bằng email @dntu.edu.vn.",
                    [nameof(Email)]);
            }

            if (string.IsNullOrWhiteSpace(StudentCode))
            {
                yield return new ValidationResult(
                    "Mã số sinh viên là bắt buộc.",
                    [nameof(StudentCode)]);
            }
        }
        else if (AccountType == RegistrationAccountType.Lecturer)
        {
            if (!IsCampusEmail())
            {
                yield return new ValidationResult(
                    "Giảng viên cần đăng ký bằng email @dntu.edu.vn.",
                    [nameof(Email)]);
            }

            if (string.IsNullOrWhiteSpace(LecturerCode))
            {
                yield return new ValidationResult(
                    "Mã giảng viên là bắt buộc.",
                    [nameof(LecturerCode)]);
            }
        }
    }

    private bool IsCampusEmail() =>
        !string.IsNullOrWhiteSpace(Email) &&
        (Email.Trim().EndsWith("@dntu.edu.vn", StringComparison.OrdinalIgnoreCase) ||
         Email.Trim().EndsWith("@skillbridge.local", StringComparison.OrdinalIgnoreCase));
}

public sealed class LoginRequest
{
    // Kept under the existing `email` JSON field for API compatibility; accepts email or student code.
    [Required, StringLength(320, MinimumLength = 1)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed class GoogleLoginRequest
{
    [Required, StringLength(8192, MinimumLength = 100)]
    public string Credential { get; init; } = string.Empty;
}

public sealed record GoogleIdentity(string Subject, string Email, string DisplayName);

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

public sealed class VerifyEmailOtpRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required, RegularExpression("^[0-9]{6}$")]
    public string Otp { get; init; } = string.Empty;
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
    bool ProfileCompleted,
    string? LecturerApprovalStatus);

public sealed record SessionResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsActive);
