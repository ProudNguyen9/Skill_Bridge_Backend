using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Students;

namespace DNTU.SkillBridge.Application.Authentication;

public interface IAuthRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<bool> StudentCodeExistsAsync(string normalizedStudentCode, CancellationToken cancellationToken);
    Task<bool> LecturerCodeExistsAsync(string normalizedLecturerCode, CancellationToken cancellationToken);
    Task<Role> FindRoleAsync(string normalizedName, CancellationToken cancellationToken);
    void AddUser(User user);
    void AddStudentProfile(StudentProfile profile);
    void AddLecturerProfile(LecturerProfile profile);
    Task<LecturerApprovalStatus?> FindLecturerApprovalStatusAsync(Guid userId, CancellationToken cancellationToken);
    Task<User?> FindUserByEmailWithPermissionsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> FindUserByGoogleSubjectWithPermissionsAsync(string googleSubjectId, CancellationToken cancellationToken);
    Task<User?> FindUserByStudentCodeWithPermissionsAsync(string normalizedStudentCode, CancellationToken cancellationToken);
    Task<RefreshToken?> FindRefreshTokenWithSessionAsync(string tokenHash, CancellationToken cancellationToken);
    void AddRefreshToken(RefreshToken token);
    Task<UserSession?> FindSessionForUpdateAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RefreshToken>> ListActiveRefreshTokensAsync(Guid sessionId, CancellationToken cancellationToken);
    Task RevokeAllSessionsAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SessionResponse>> ListSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<User?> FindUserWithPermissionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<Guid?> FindStudentProfileIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<Guid?> FindCompanyIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<Guid?> FindLecturerProfileIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> IsStudentProfileCompletedAsync(Guid studentProfileId, CancellationToken cancellationToken);
    void AddSession(UserSession session);
    Task<User?> FindUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AccountToken>> ListActiveAccountTokensAsync(Guid userId, AccountTokenPurpose purpose, CancellationToken cancellationToken);
    Task<AccountToken?> FindLatestActiveAccountTokenWithUserAsync(Guid userId, AccountTokenPurpose purpose, CancellationToken cancellationToken);
    void AddAccountToken(AccountToken token);
    Task<AccountToken?> FindAccountTokenWithUserAsync(string tokenHash, AccountTokenPurpose purpose, CancellationToken cancellationToken);
    Task<User?> FindUserForPasswordChangeAsync(Guid userId, CancellationToken cancellationToken);
}
