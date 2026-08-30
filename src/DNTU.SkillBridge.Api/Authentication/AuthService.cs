using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DNTU.SkillBridge.Api.Authentication;

public sealed class AuthService(
    AppDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions)
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<bool> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return false;
        }

        var roleName = request.AccountType == RegistrationAccountType.Student ? RoleNames.Student : RoleNames.Company;
        var role = await dbContext.Roles.SingleAsync(item => item.NormalizedName == roleName, cancellationToken);
        var user = new User(request.Email, request.DisplayName, string.Empty);
        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
        user.AssignRole(role.Id);

        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The unique normalized-email index is the final authority under concurrent registration.
            return false;
        }
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await LoadUserByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<AuthResponse?> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(rawRefreshToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var refreshToken = await dbContext.RefreshTokens
            .AsTracking()
            .Include(token => token.Session)
            .ThenInclude(session => session.User)
            .ThenInclude(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .ThenInclude(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (!refreshToken.IsUsable(now) || !refreshToken.Session.IsActive(now) || !refreshToken.Session.User.IsActive)
        {
            if (refreshToken.UsedAt is not null)
            {
                await RevokeFamilyAsync(refreshToken.Session.FamilyId, now, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
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
        dbContext.RefreshTokens.Add(replacement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await CreateAuthResponseAsync(refreshToken.Session.User, refreshToken.Session, replacementRawToken, cancellationToken);
    }

    public async Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var session = await dbContext.UserSessions.AsTracking().SingleOrDefaultAsync(
            item => item.Id == sessionId && item.UserId == userId,
            cancellationToken);
        if (session is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        session.Revoke(now);
        foreach (var token in await dbContext.RefreshTokens.AsTracking().Where(token => token.SessionId == session.Id && token.RevokedAt == null).ToListAsync(cancellationToken))
        {
            token.Revoke(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await RevokeAllSessionsCoreAsync(userId, DateTimeOffset.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SessionResponse>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await dbContext.UserSessions
            .Where(session => session.UserId == userId)
            .OrderByDescending(session => session.CreatedAt)
            .Select(session => new SessionResponse(session.Id, session.CreatedAt, session.ExpiresAt, session.RevokedAt == null && session.ExpiresAt > now))
            .ToListAsync(cancellationToken);
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .ThenInclude(item => item.RolePermissions)
            .ThenInclude(item => item.Permission)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = user.UserRoles.Select(item => item.Role.Name).Order().ToArray();
        var permissions = user.UserRoles.SelectMany(item => item.Role.RolePermissions).Select(item => item.Permission.Name).Distinct().Order().ToArray();
        var studentProfileId = await dbContext.StudentProfiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var companyId = await dbContext.CompanyMembers
            .Where(member => member.UserId == userId)
            .Select(member => (Guid?)member.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);
        var lecturerProfileId = await dbContext.LecturerProfiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var profileType = studentProfileId.HasValue
            ? "STUDENT"
            : companyId.HasValue
                ? "COMPANY"
                : lecturerProfileId.HasValue ? "LECTURER" : null;
        var profileCompleted = studentProfileId.HasValue && await dbContext.StudentProfiles
            .Where(profile => profile.Id == studentProfileId.Value)
            .Select(profile => profile.StudentCode != null && profile.MajorId != null && profile.FacultyId != null)
            .SingleOrDefaultAsync(cancellationToken);
        return new CurrentUserResponse(user.Id, user.Email, user.DisplayName, user.EmailVerified, user.IsActive, roles, permissions, profileType, studentProfileId, companyId, lecturerProfileId, profileCompleted);
    }

    private async Task<AuthResponse> CreateSessionAsync(User user, CancellationToken cancellationToken)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);
        var session = new UserSession(user.Id, expiresAt);
        var rawRefreshToken = CreateRawRefreshToken();
        session.RefreshTokens.Add(new RefreshToken(session.Id, HashToken(rawRefreshToken), 1, expiresAt));
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await CreateAuthResponseAsync(user, session, rawRefreshToken, cancellationToken);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, UserSession session, string rawRefreshToken, CancellationToken cancellationToken)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var accessTokenExpiresAt = issuedAt.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var studentProfileId = await dbContext.StudentProfiles
            .Where(profile => profile.UserId == user.Id)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var companyMembershipId = await dbContext.CompanyMembers
            .Where(member => member.UserId == user.Id)
            .Select(member => (Guid?)member.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);
        var lecturerProfileId = await dbContext.LecturerProfiles
            .Where(profile => profile.UserId == user.Id)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
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
        await dbContext.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .ThenInclude(item => item.RolePermissions)
            .ThenInclude(item => item.Permission)
            .SingleOrDefaultAsync(item => item.NormalizedEmail == email.Trim().ToUpperInvariant(), cancellationToken);

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.UserSessions.AsTracking().Where(session => session.FamilyId == familyId).ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(revokedAt);
        }

        var sessionIds = sessions.Select(session => session.Id).ToArray();
        foreach (var token in await dbContext.RefreshTokens.AsTracking().Where(token => sessionIds.Contains(token.SessionId) && token.RevokedAt == null).ToListAsync(cancellationToken))
        {
            token.Revoke(revokedAt);
        }
    }

    private static string CreateRawRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public async Task<string?> CreateAccountTokenAsync(string email, AccountTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.NormalizedEmail == email.Trim().ToUpperInvariant(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var activeTokens = await dbContext.AccountTokens.AsTracking()
            .Where(item => item.UserId == user.Id && item.Purpose == purpose && item.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        if (purpose == AccountTokenPurpose.EmailVerification && activeTokens.Any(item => item.CreatedAt > now.AddMinutes(-1)))
        {
            return null;
        }

        var rawToken = CreateRawRefreshToken();
        var expiry = now.AddHours(purpose == AccountTokenPurpose.EmailVerification ? 24 : 1);
        foreach (var existing in activeTokens)
        {
            existing.Consume(now);
        }

        dbContext.AccountTokens.Add(new AccountToken(user.Id, purpose, HashToken(rawToken), expiry));
        await dbContext.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public async Task<bool> VerifyEmailAsync(string rawToken, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var token = await dbContext.AccountTokens.Include(item => item.User).AsTracking().SingleOrDefaultAsync(
            item => item.TokenHash == HashToken(rawToken) && item.Purpose == AccountTokenPurpose.EmailVerification,
            cancellationToken);
        if (token is null || !token.IsUsable(now))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        token.Consume(now);
        token.User.SetEmailVerified();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(string rawToken, string newPassword, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var token = await dbContext.AccountTokens.Include(item => item.User).AsTracking().SingleOrDefaultAsync(
            item => item.TokenHash == HashToken(rawToken) && item.Purpose == AccountTokenPurpose.PasswordReset,
            cancellationToken);
        if (token is null || !token.IsUsable(now) || !token.User.IsActive)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        token.Consume(now);
        token.User.SetPasswordHash(passwordHasher.HashPassword(token.User, newPassword));
        await RevokeAllSessionsCoreAsync(token.UserId, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        user.SetPasswordHash(passwordHasher.HashPassword(user, newPassword));
        await RevokeAllSessionsCoreAsync(userId, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task RevokeAllSessionsCoreAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await dbContext.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);
        await dbContext.RefreshTokens
            .Where(token => token.Session.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
