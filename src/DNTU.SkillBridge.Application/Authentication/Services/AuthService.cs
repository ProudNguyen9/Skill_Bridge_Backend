using System.IdentityModel.Tokens.Jwt;
using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Students;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DNTU.SkillBridge.Application.Authentication;

public sealed class AuthService(
    IAuthRepository authRepository,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    IUnitOfWork unitOfWork) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<bool> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var normalizedStudentCode = request.StudentCode?.Trim().ToUpperInvariant();
        var isDntuEmail = normalizedEmail.EndsWith("@DNTU.EDU.VN", StringComparison.Ordinal) ||
                          normalizedEmail.EndsWith("@SKILLBRIDGE.LOCAL", StringComparison.Ordinal);
        if (request.AccountType == RegistrationAccountType.Student &&
            (!isDntuEmail || string.IsNullOrWhiteSpace(normalizedStudentCode)))
        {
            return false;
        }

        if (await authRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            return false;
        }

        if (request.AccountType == RegistrationAccountType.Student &&
            await authRepository.StudentCodeExistsAsync(normalizedStudentCode!, cancellationToken))
        {
            return false;
        }

        var roleName = request.AccountType == RegistrationAccountType.Student ? RoleNames.Student : RoleNames.Company;
        var role = await authRepository.FindRoleAsync(roleName, cancellationToken);
        var user = new User(request.Email, request.DisplayName, string.Empty);
        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
        user.AssignRole(role.Id);
        if (request.Email.EndsWith("@skillbridge.local", StringComparison.OrdinalIgnoreCase))
        {
            user.SetEmailVerified();
        }

        authRepository.AddUser(user);
        if (request.AccountType == RegistrationAccountType.Student)
        {
            var studentProfile = new StudentProfile(user.Id);
            studentProfile.Update(normalizedStudentCode, null, null, null, null, null, null, null, null, null);
            authRepository.AddStudentProfile(studentProfile);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (PersistenceException)
        {
            // Unique email and student-code indexes are the final authority under concurrent registration.
            return false;
        }
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var identifier = request.Email.Trim();
        var user = identifier.Contains('@')
            ? await LoadUserByEmailAsync(identifier, cancellationToken)
            : await authRepository.FindUserByStudentCodeWithPermissionsAsync(identifier.ToUpperInvariant(), cancellationToken);
        if (user is null || !user.IsActive || !user.EmailVerified || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<AuthResponse?> LoginWithGoogleAsync(GoogleIdentity identity, CancellationToken cancellationToken)
    {
        var subject = identity.Subject.Trim();
        var email = identity.Email.Trim();
        if (subject.Length == 0 || !email.EndsWith("@dntu.edu.vn", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var user = await authRepository.FindUserByGoogleSubjectWithPermissionsAsync(subject, cancellationToken);
        if (user is null)
        {
            user = await LoadUserByEmailAsync(email, cancellationToken);
            if (user is null || !user.IsActive || !user.LinkGoogleIdentity(subject))
            {
                return null;
            }

            // Google only reaches this point after the signed ID token confirms a verified school account.
            user.SetEmailVerified();
        }

        if (!user.IsActive)
        {
            return null;
        }

        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<AuthResponse?> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(rawRefreshToken);
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var refreshToken = await authRepository.FindRefreshTokenWithSessionAsync(tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (!refreshToken.IsUsable(now) || !refreshToken.Session.IsActive(now) || !refreshToken.Session.User.IsActive)
        {
            if (refreshToken.UsedAt is not null)
            {
                await authRepository.RevokeFamilyAsync(refreshToken.Session.FamilyId, now, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var replacementRawToken = CreateRawRefreshToken();
        var replacement = new RefreshToken(
            refreshToken.SessionId,
            HashToken(replacementRawToken),
            refreshToken.Sequence + 1,
            refreshToken.Session.ExpiresAt);
        refreshToken.MarkUsed(replacement.Id, now);
        authRepository.AddRefreshToken(replacement);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await CreateAuthResponseAsync(refreshToken.Session.User, refreshToken.Session, replacementRawToken, cancellationToken);
    }

    public async Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var session = await authRepository.FindSessionForUpdateAsync(userId, sessionId, cancellationToken);
        if (session is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        session.Revoke(now);
        foreach (var token in await authRepository.ListActiveRefreshTokensAsync(session.Id, cancellationToken))
        {
            token.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await authRepository.RevokeAllSessionsAsync(userId, DateTimeOffset.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SessionResponse>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await authRepository.ListSessionsAsync(userId, now, cancellationToken);
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await authRepository.FindUserWithPermissionsAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = user.UserRoles.Select(item => item.Role.Name).Order().ToArray();
        var permissions = user.UserRoles.SelectMany(item => item.Role.RolePermissions).Select(item => item.Permission.Name).Distinct().Order().ToArray();
        var studentProfileId = await authRepository.FindStudentProfileIdAsync(userId, cancellationToken);
        var companyId = await authRepository.FindCompanyIdAsync(userId, cancellationToken);
        var lecturerProfileId = await authRepository.FindLecturerProfileIdAsync(userId, cancellationToken);
        var profileType = studentProfileId.HasValue
            ? "STUDENT"
            : companyId.HasValue
                ? "COMPANY"
                : lecturerProfileId.HasValue ? "LECTURER" : null;
        var profileCompleted = studentProfileId.HasValue && await authRepository.IsStudentProfileCompletedAsync(studentProfileId.Value, cancellationToken);
        return new CurrentUserResponse(user.Id, user.Email, user.DisplayName, user.EmailVerified, user.IsActive, roles, permissions, profileType, studentProfileId, companyId, lecturerProfileId, profileCompleted);
    }

    private async Task<AuthResponse> CreateSessionAsync(User user, CancellationToken cancellationToken)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);
        var session = new UserSession(user.Id, expiresAt);
        var rawRefreshToken = CreateRawRefreshToken();
        session.RefreshTokens.Add(new RefreshToken(session.Id, HashToken(rawRefreshToken), 1, expiresAt));
        authRepository.AddSession(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await CreateAuthResponseAsync(user, session, rawRefreshToken, cancellationToken);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, UserSession session, string rawRefreshToken, CancellationToken cancellationToken)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var accessTokenExpiresAt = issuedAt.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var studentProfileId = await authRepository.FindStudentProfileIdAsync(user.Id, cancellationToken);
        var companyMembershipId = await authRepository.FindCompanyIdAsync(user.Id, cancellationToken);
        var lecturerProfileId = await authRepository.FindLecturerProfileIdAsync(user.Id, cancellationToken);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("session_id", session.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email)
        };
        if (studentProfileId.HasValue)
        {
            claims.Add(new Claim("student_id", studentProfileId.Value.ToString()));
        }

        if (companyMembershipId.HasValue)
        {
            claims.Add(new Claim("company_id", companyMembershipId.Value.ToString()));
        }

        if (lecturerProfileId.HasValue)
        {
            claims.Add(new Claim("lecturer_id", lecturerProfileId.Value.ToString()));
        }
        foreach (var role in user.UserRoles.Select(item => item.Role.Name).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in user.UserRoles.SelectMany(item => item.Role.RolePermissions).Select(item => item.Permission.Name).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim("permission", permission));
        }

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)), SecurityAlgorithms.HmacSha512);
        var jwt = new JwtSecurityToken(_jwtOptions.Issuer, _jwtOptions.Audience, claims, issuedAt.UtcDateTime, accessTokenExpiresAt.UtcDateTime, credentials);
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(jwt), accessTokenExpiresAt, rawRefreshToken, session.ExpiresAt, session.Id);
    }

    private async Task<User?> LoadUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        await authRepository.FindUserByEmailWithPermissionsAsync(email.Trim().ToUpperInvariant(), cancellationToken);

    private static string CreateRawRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public async Task<string?> CreateAccountTokenAsync(string email, AccountTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var user = await authRepository.FindUserByEmailAsync(email.Trim().ToUpperInvariant(), cancellationToken);
        if (user is null || !user.IsActive || (purpose == AccountTokenPurpose.EmailVerification && user.EmailVerified))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var activeTokens = await authRepository.ListActiveAccountTokensAsync(user.Id, purpose, cancellationToken);
        if (purpose == AccountTokenPurpose.EmailVerification && activeTokens.Any(item => item.CreatedAt > now.AddMinutes(-1)))
        {
            return null;
        }

        var rawToken = purpose == AccountTokenPurpose.EmailVerification
            ? CreateVerificationCode()
            : CreateRawRefreshToken();
        var tokenHash = purpose == AccountTokenPurpose.EmailVerification
            ? HashVerificationCode(rawToken)
            : HashToken(rawToken);
        var expiry = purpose == AccountTokenPurpose.EmailVerification ? now.AddMinutes(10) : now.AddHours(1);
        foreach (var existing in activeTokens)
        {
            existing.Consume(now);
        }

        authRepository.AddAccountToken(new AccountToken(user.Id, purpose, tokenHash, expiry));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public async Task<bool> VerifyEmailAsync(string email, string otp, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await authRepository.FindUserByEmailAsync(email.Trim().ToUpperInvariant(), cancellationToken);
        if (user is null || !user.IsActive || user.EmailVerified)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var token = await authRepository.FindLatestActiveAccountTokenWithUserAsync(
            user.Id,
            AccountTokenPurpose.EmailVerification,
            cancellationToken);
        if (token is null || !token.IsUsable(now))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var providedHash = HashVerificationCode(otp);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(providedHash),
                Encoding.ASCII.GetBytes(token.TokenHash)))
        {
            token.RegisterFailedAttempt(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        token.Consume(now);
        user.SetEmailVerified();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(string rawToken, string newPassword, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var token = await authRepository.FindAccountTokenWithUserAsync(HashToken(rawToken), AccountTokenPurpose.PasswordReset, cancellationToken);
        if (token is null || !token.IsUsable(now) || !token.User.IsActive)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        token.Consume(now);
        token.User.SetPasswordHash(passwordHasher.HashPassword(token.User, newPassword));
        await authRepository.RevokeAllSessionsAsync(token.UserId, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await authRepository.FindUserForPasswordChangeAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        user.SetPasswordHash(passwordHasher.HashPassword(user, newPassword));
        await authRepository.RevokeAllSessionsAsync(userId, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private string HashVerificationCode(string code) => Convert.ToHexString(
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey), Encoding.UTF8.GetBytes(code)));

    private static string CreateVerificationCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
}
