using DNTU.SkillBridge.Application.Authentication;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

public sealed class AuthRepository(AppDbContext dbContext) : IAuthRepository
{
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<Role> FindRoleAsync(string normalizedName, CancellationToken cancellationToken) =>
        dbContext.Roles.SingleAsync(item => item.NormalizedName == normalizedName, cancellationToken);

    public void AddUser(User user) => dbContext.Users.Add(user);

    public Task<User?> FindUserByEmailWithPermissionsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .ThenInclude(item => item.RolePermissions)
            .ThenInclude(item => item.Permission)
            .SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<RefreshToken?> FindRefreshTokenWithSessionAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .AsTracking()
            .Include(token => token.Session)
            .ThenInclude(session => session.User)
            .ThenInclude(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .ThenInclude(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public void AddRefreshToken(RefreshToken token) => dbContext.RefreshTokens.Add(token);

    public Task<UserSession?> FindSessionForUpdateAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        dbContext.UserSessions
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);

    public async Task<IReadOnlyCollection<RefreshToken>> ListActiveRefreshTokensAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await dbContext.RefreshTokens
            .AsTracking()
            .Where(token => token.SessionId == sessionId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

    public async Task RevokeAllSessionsAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        await dbContext.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, revokedAt), cancellationToken);
        await dbContext.RefreshTokens
            .Where(token => token.Session.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, revokedAt), cancellationToken);
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
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

    public async Task<IReadOnlyCollection<SessionResponse>> ListSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.UserSessions
            .Where(session => session.UserId == userId)
            .OrderByDescending(session => session.CreatedAt)
            .Select(session => new SessionResponse(session.Id, session.CreatedAt, session.ExpiresAt, session.RevokedAt == null && session.ExpiresAt > now))
            .ToListAsync(cancellationToken);

    public Task<User?> FindUserWithPermissionsAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .ThenInclude(item => item.RolePermissions)
            .ThenInclude(item => item.Permission)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);

    public async Task<Guid?> FindStudentProfileIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.StudentProfiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Guid?> FindCompanyIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.CompanyMembers
            .Where(member => member.UserId == userId)
            .Select(member => (Guid?)member.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Guid?> FindLecturerProfileIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.LecturerProfiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> IsStudentProfileCompletedAsync(Guid studentProfileId, CancellationToken cancellationToken) =>
        await dbContext.StudentProfiles
            .Where(profile => profile.Id == studentProfileId)
            .Select(profile => profile.StudentCode != null && profile.MajorId != null && profile.FacultyId != null)
            .SingleOrDefaultAsync(cancellationToken);

    public void AddSession(UserSession session) => dbContext.UserSessions.Add(session);

    public Task<User?> FindUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);

    public async Task<IReadOnlyCollection<AccountToken>> ListActiveAccountTokensAsync(Guid userId, AccountTokenPurpose purpose, CancellationToken cancellationToken) =>
        await dbContext.AccountTokens
            .AsTracking()
            .Where(item => item.UserId == userId && item.Purpose == purpose && item.ConsumedAt == null)
            .ToListAsync(cancellationToken);

    public void AddAccountToken(AccountToken token) => dbContext.AccountTokens.Add(token);

    public Task<AccountToken?> FindAccountTokenWithUserAsync(string tokenHash, AccountTokenPurpose purpose, CancellationToken cancellationToken) =>
        dbContext.AccountTokens
            .Include(item => item.User)
            .AsTracking()
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash && item.Purpose == purpose, cancellationToken);

    public Task<User?> FindUserForPasswordChangeAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
}
