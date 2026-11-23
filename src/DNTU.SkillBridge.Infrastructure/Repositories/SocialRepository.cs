using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Social;
using DNTU.SkillBridge.Application.Social.Repositories;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Social;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation của mạng lưới theo dõi & bảng tin.</summary>
public sealed class SocialRepository(AppDbContext dbContext) : ISocialRepository
{
    private static readonly ProjectStatus[] RecruitingStatuses = [ProjectStatus.APPROVED, ProjectStatus.RECRUITING];
    private static readonly ProjectStatus[] VisibleStatuses = [ProjectStatus.APPROVED, ProjectStatus.RECRUITING, ProjectStatus.IN_PROGRESS, ProjectStatus.COMPLETED];

    // ---------- Follow ----------

    public Task<UserFollow?> GetFollowAsync(Guid followerUserId, Guid followeeUserId, CancellationToken cancellationToken) =>
        dbContext.UserFollows.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FollowerUserId == followerUserId && f.FolloweeUserId == followeeUserId, cancellationToken);

    public Task AddFollowAsync(UserFollow follow, CancellationToken cancellationToken)
    {
        dbContext.UserFollows.Add(follow);
        return Task.CompletedTask;
    }

    public async Task<bool> RemoveFollowAsync(Guid followerUserId, Guid followeeUserId, CancellationToken cancellationToken)
    {
        var follow = await dbContext.UserFollows
            .FirstOrDefaultAsync(f => f.FollowerUserId == followerUserId && f.FolloweeUserId == followeeUserId, cancellationToken);
        if (follow is null) return false;
        dbContext.UserFollows.Remove(follow);
        return true;
    }

    public Task<bool> IsFollowingAsync(Guid followerUserId, Guid followeeUserId, CancellationToken cancellationToken) =>
        dbContext.UserFollows.AsNoTracking()
            .AnyAsync(f => f.FollowerUserId == followerUserId && f.FolloweeUserId == followeeUserId, cancellationToken);

    public Task<bool> IsFollowedByAsync(Guid userId, Guid followerUserId, CancellationToken cancellationToken) =>
        dbContext.UserFollows.AsNoTracking()
            .AnyAsync(f => f.FollowerUserId == followerUserId && f.FolloweeUserId == userId, cancellationToken);

    public Task<int> CountFollowersAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.UserFollows.AsNoTracking().CountAsync(f => f.FolloweeUserId == userId, cancellationToken);

    public Task<int> CountFollowingAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.UserFollows.AsNoTracking().CountAsync(f => f.FollowerUserId == userId, cancellationToken);

    public Task<IReadOnlyList<Guid>> GetFolloweeIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        FolloweeIdsCore(userId);

    private async Task<IReadOnlyList<Guid>> FolloweeIdsCore(Guid userId)
    {
        var ids = await dbContext.UserFollows.AsNoTracking()
            .Where(f => f.FollowerUserId == userId)
            .Select(f => f.FolloweeUserId)
            .ToListAsync();
        return ids;
    }

    public async Task<IReadOnlyList<SocialNetworkRow>> ListNetworkAsync(Guid userId, string direction, string? roleFilter, int skip, int take, CancellationToken cancellationToken)
    {
        var normalizedDirection = direction?.ToUpperInvariant() == "FOLLOWING" ? "FOLLOWING" : "FOLLOWERS";
        var query = normalizedDirection == "FOLLOWING"
            ? dbContext.UserFollows.AsNoTracking().Where(f => f.FollowerUserId == userId)
            : dbContext.UserFollows.AsNoTracking().Where(f => f.FolloweeUserId == userId);

        var rows = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(f => new { OtherUserId = normalizedDirection == "FOLLOWING" ? f.FolloweeUserId : f.FollowerUserId, f.CreatedAt })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return [];

        var result = new List<SocialNetworkRow>(rows.Count);
        foreach (var row in rows)
        {
            var basics = await GetUserBasicsCoreAsync(row.OtherUserId, cancellationToken);
            if (basics is null) continue;
            if (roleFilter is not null && !string.Equals(basics.Value.Role, roleFilter, StringComparison.OrdinalIgnoreCase)) continue;

            result.Add(new SocialNetworkRow(
                basics.Value.UserId,
                basics.Value.DisplayName,
                basics.Value.AvatarUrl,
                basics.Value.Role,
                basics.Value.Detail,
                await IsFollowingAsync(userId, row.OtherUserId, cancellationToken),
                row.CreatedAt));
        }

        return result;
    }

    public async Task<int> CountNetworkAsync(Guid userId, string direction, string? roleFilter, CancellationToken cancellationToken)
    {
        // roleFilter được áp dụng sau khi resolve vai trò ở ListNetwork; đếm gần đúng theo hướng.
        var normalizedDirection = direction?.ToUpperInvariant() == "FOLLOWING" ? "FOLLOWING" : "FOLLOWERS";
        if (string.IsNullOrWhiteSpace(roleFilter))
        {
            return normalizedDirection == "FOLLOWING"
                ? await dbContext.UserFollows.AsNoTracking().CountAsync(f => f.FollowerUserId == userId, cancellationToken)
                : await dbContext.UserFollows.AsNoTracking().CountAsync(f => f.FolloweeUserId == userId, cancellationToken);
        }

        var otherIds = normalizedDirection == "FOLLOWING"
            ? await dbContext.UserFollows.AsNoTracking().Where(f => f.FollowerUserId == userId).Select(f => f.FolloweeUserId).ToListAsync(cancellationToken)
            : await dbContext.UserFollows.AsNoTracking().Where(f => f.FolloweeUserId == userId).Select(f => f.FollowerUserId).ToListAsync(cancellationToken);
        if (otherIds.Count == 0) return 0;

        var count = 0;
        foreach (var otherId in otherIds)
        {
            var basics = await GetUserBasicsCoreAsync(otherId, cancellationToken);
            if (basics.HasValue && string.Equals(basics.Value.Role, roleFilter, StringComparison.OrdinalIgnoreCase)) count++;
        }

        return count;
    }

    // ---------- Block ----------

    public Task<bool> IsBlockedBetweenAsync(Guid userA, Guid userB, CancellationToken cancellationToken) =>
        dbContext.UserBlocks.AsNoTracking()
            .AnyAsync(b => (b.BlockerUserId == userA && b.BlockedUserId == userB) || (b.BlockerUserId == userB && b.BlockedUserId == userA), cancellationToken);

    public Task AddBlockAsync(UserBlock block, CancellationToken cancellationToken)
    {
        dbContext.UserBlocks.Add(block);
        return Task.CompletedTask;
    }

    public async Task<bool> RemoveBlockAsync(Guid blockerUserId, Guid blockedUserId, CancellationToken cancellationToken)
    {
        var block = await dbContext.UserBlocks
            .FirstOrDefaultAsync(b => b.BlockerUserId == blockerUserId && b.BlockedUserId == blockedUserId, cancellationToken);
        if (block is null) return false;
        dbContext.UserBlocks.Remove(block);
        return true;
    }

    public async Task RemoveFollowsBetweenAsync(Guid userA, Guid userB, CancellationToken cancellationToken)
    {
        var follows = await dbContext.UserFollows
            .Where(f => (f.FollowerUserId == userA && f.FolloweeUserId == userB) || (f.FollowerUserId == userB && f.FolloweeUserId == userA))
            .ToListAsync(cancellationToken);
        dbContext.UserFollows.RemoveRange(follows);
    }

    // ---------- Feed ----------

    public async Task<IReadOnlyList<SocialActivityRow>> ListActivitiesAsync(IReadOnlyCollection<Guid> actorIds, int skip, int take, CancellationToken cancellationToken)
    {
        if (actorIds.Count == 0) return [];
        var rows = await dbContext.SocialActivities.AsNoTracking()
            .Where(a => actorIds.Contains(a.ActorUserId))
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(a => new
            {
                a.Id,
                a.ActivityType,
                a.TargetEntityId,
                a.Title,
                a.Summary,
                a.LinkUrl,
                a.CreatedAt,
                a.ActorUserId,
                ActorName = dbContext.Users.Where(u => u.Id == a.ActorUserId).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var result = new List<SocialActivityRow>(rows.Count);
        foreach (var row in rows)
        {
            var basics = await GetUserBasicsCoreAsync(row.ActorUserId, cancellationToken);
            result.Add(new SocialActivityRow(
                row.Id,
                row.ActivityType,
                row.TargetEntityId,
                row.Title,
                row.Summary,
                row.LinkUrl,
                row.CreatedAt,
                row.ActorUserId,
                row.ActorName ?? basics?.DisplayName ?? string.Empty,
                basics?.AvatarUrl,
                basics?.Role));
        }

        return result;
    }

    public async Task<int> CountActivitiesAsync(IReadOnlyCollection<Guid> actorIds, CancellationToken cancellationToken)
    {
        if (actorIds.Count == 0) return 0;
        return await dbContext.SocialActivities.AsNoTracking()
            .CountAsync(a => actorIds.Contains(a.ActorUserId), cancellationToken);
    }

    // ---------- Gợi ý kết nối ----------

    public async Task<IReadOnlyList<SocialSuggestionRow>> GetSuggestionsAsync(Guid viewerUserId, int limit, CancellationToken cancellationToken)
    {
        var excluded = new HashSet<Guid> { viewerUserId };
        excluded.UnionWith(await FolloweeIdsCore(viewerUserId));
        var blockedIds = await dbContext.UserBlocks.AsNoTracking()
            .Where(b => b.BlockerUserId == viewerUserId || b.BlockedUserId == viewerUserId)
            .Select(b => b.BlockerUserId == viewerUserId ? b.BlockedUserId : b.BlockerUserId)
            .ToListAsync(cancellationToken);
        excluded.UnionWith(blockedIds);
        var adminIds = await dbContext.Users.AsNoTracking()
            .Where(u => u.UserRoles.Any(r => r.Role.Name == RoleNames.Admin || r.Role.Name == RoleNames.SuperAdmin))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        excluded.UnionWith(adminIds);

        var role = await ResolveRoleAsync(viewerUserId, cancellationToken);
        var candidates = new List<(Guid UserId, string Reason, int SharedCount)>();

        if (role == RoleNames.Student)
        {
            var mySkillIds = await StudentSkillIdsAsync(viewerUserId, cancellationToken);
            if (mySkillIds.Count > 0)
            {
                // Doanh nghiệp có đề tài khớp kỹ năng.
                var companyProjectCounts = await dbContext.Projects.AsNoTracking()
                    .Where(p => RecruitingStatuses.Contains(p.Status)
                        && dbContext.ProjectSkills.Any(ps => ps.ProjectId == p.Id && mySkillIds.Contains(ps.SkillId)))
                    .Select(p => p.CompanyId)
                    .ToListAsync(cancellationToken);
                var companyUsers = await dbContext.CompanyMembers.AsNoTracking()
                    .Where(m => companyProjectCounts.Distinct().Contains(m.CompanyId))
                    .Select(m => m.UserId)
                    .ToListAsync(cancellationToken);
                foreach (var companyUser in companyUsers.Distinct())
                {
                    var companyProjects = await dbContext.Projects.AsNoTracking()
                        .CountAsync(p => RecruitingStatuses.Contains(p.Status)
                            && p.Company.Members.Any(m => m.UserId == companyUser)
                            && dbContext.ProjectSkills.Any(ps => ps.ProjectId == p.Id && mySkillIds.Contains(ps.SkillId)), cancellationToken);
                    candidates.Add((companyUser, $"Có {companyProjects} đề tài khớp kỹ năng của bạn", companyProjects));
                }

                // Sinh viên khác có kỹ năng chung.
                var students = await dbContext.StudentSkills.AsNoTracking()
                    .Join(dbContext.StudentProfiles.AsNoTracking(),
                        ss => ss.StudentId,
                        sp => sp.Id,
                        (ss, sp) => new { ss.SkillId, sp.UserId })
                    .Where(x => mySkillIds.Contains(x.SkillId) && x.UserId != viewerUserId)
                    .GroupBy(x => x.UserId)
                    .Select(g => new { UserId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync(cancellationToken);
                candidates.AddRange(students.Select(s => (UserId: s.UserId, Reason: $"Cùng {s.Count} kỹ năng với bạn", SharedCount: s.Count)));
            }

            // Giảng viên đang hướng dẫn đề tài khớp kỹ năng.
                var lecturerUsers = await dbContext.LecturerAssignments.AsNoTracking()
                .Where(a => a.Status == LecturerAssignmentStatus.ACTIVE
                    && dbContext.Projects.AsNoTracking().Any(p => p.Id == a.ProjectId
                        && dbContext.ProjectSkills.Any(ps => ps.ProjectId == p.Id && mySkillIds.Contains(ps.SkillId))))
                .Join(dbContext.LecturerProfiles.AsNoTracking(),
                    a => a.LecturerId,
                    l => l.Id,
                    (a, l) => l.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            candidates.AddRange(lecturerUsers.Select(u => (u, "Giảng viên hướng dẫn đề tài khớp kỹ năng của bạn", 1)));
        }
        else if (role == RoleNames.Company)
        {
            var mySkillIds = await CompanyProjectSkillIdsAsync(viewerUserId, cancellationToken);
            if (mySkillIds.Count > 0)
            {
                var students = await dbContext.StudentSkills.AsNoTracking()
                    .Join(dbContext.StudentProfiles.AsNoTracking(),
                        ss => ss.StudentId,
                        sp => sp.Id,
                        (ss, sp) => new { ss.SkillId, sp.UserId })
                    .Where(x => mySkillIds.Contains(x.SkillId))
                    .GroupBy(x => x.UserId)
                    .Select(g => new { UserId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync(cancellationToken);
                candidates.AddRange(students.Select(s => (UserId: s.UserId, Reason: $"Sinh viên có {s.Count} kỹ năng khớp đề tài của bạn", SharedCount: s.Count)));

                var lecturerUsers = await dbContext.LecturerAssignments.AsNoTracking()
                    .Where(a => a.Status == LecturerAssignmentStatus.ACTIVE
                        && dbContext.Projects.AsNoTracking().Any(p => p.Id == a.ProjectId && p.Company.Members.Any(m => m.UserId == viewerUserId)
                            && dbContext.ProjectSkills.Any(ps => ps.ProjectId == p.Id && mySkillIds.Contains(ps.SkillId))))
                    .Join(dbContext.LecturerProfiles.AsNoTracking(),
                        a => a.LecturerId,
                        l => l.Id,
                        (a, l) => l.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                candidates.AddRange(lecturerUsers.Select(u => (u, "Giảng viên đang hướng dẫn đề tài của doanh nghiệp bạn", 1)));
            }
        }
        else if (role == RoleNames.Lecturer)
        {
            var mySkillIds = await LecturerProjectSkillIdsAsync(viewerUserId, cancellationToken);
            if (mySkillIds.Count > 0)
            {
                var students = await dbContext.StudentSkills.AsNoTracking()
                    .Join(dbContext.StudentProfiles.AsNoTracking(),
                        ss => ss.StudentId,
                        sp => sp.Id,
                        (ss, sp) => new { ss.SkillId, sp.UserId })
                    .Where(x => mySkillIds.Contains(x.SkillId))
                    .GroupBy(x => x.UserId)
                    .Select(g => new { UserId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync(cancellationToken);
                candidates.AddRange(students.Select(s => (UserId: s.UserId, Reason: $"Sinh viên có {s.Count} kỹ năng khớp đề tài bạn hướng dẫn", SharedCount: s.Count)));
            }

            var recruitingCompanies = await dbContext.Projects.AsNoTracking()
                .Where(p => RecruitingStatuses.Contains(p.Status))
                .Select(p => p.Company.Members.OrderBy(m => m.Role == CompanyMemberRole.OWNER ? 0 : 1).Select(m => (Guid?)m.UserId).FirstOrDefault())
                .ToListAsync(cancellationToken);
            candidates.AddRange(recruitingCompanies
                .Where(u => u.HasValue && u.Value != Guid.Empty)
                .Select(u => (UserId: u.Value, Reason: "Doanh nghiệp đang có đề tài tuyển", SharedCount: 1)));
        }

        var seen = new HashSet<Guid>();
        var result = new List<SocialSuggestionRow>();
        foreach (var (userId, reason, count) in candidates.OrderByDescending(c => c.Item3))
        {
            if (excluded.Contains(userId) || !seen.Add(userId)) continue;
            var basics = await GetUserBasicsCoreAsync(userId, cancellationToken);
            if (basics is null) continue;
            result.Add(new SocialSuggestionRow(
                basics.Value.UserId,
                basics.Value.DisplayName,
                basics.Value.AvatarUrl,
                basics.Value.Role,
                reason,
                count,
                false));
            if (result.Count >= limit) break;
        }

        return result;
    }

    // ---------- Tiện ích ----------

    public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken);

    public Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .AnyAsync(u => u.UserRoles.Any(r => r.Role.Name == RoleNames.Admin || r.Role.Name == RoleNames.SuperAdmin), cancellationToken);

    public async Task<(Guid UserId, string DisplayName, string? AvatarUrl, string Role)?> GetUserBasicsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var basics = await GetUserBasicsCoreAsync(userId, cancellationToken);
        return basics is null ? null : (basics.Value.UserId, basics.Value.DisplayName, basics.Value.AvatarUrl, basics.Value.Role);
    }

    /// <inheritdoc />
    public async Task<SocialUserProfileData?> GetUserProfileDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null) return null;

        var student = await dbContext.StudentProfiles.AsNoTracking()
            .Where(sp => sp.UserId == userId)
            .Select(sp => new { sp.StudentCode, sp.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);
        if (student is not null)
        {
            return new SocialUserProfileData(user.Id, user.DisplayName, student.AvatarUrl, RoleNames.Student, student.StudentCode);
        }

        var lecturer = await dbContext.LecturerProfiles.AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => new { l.AcademicTitle, l.Department, l.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);
        if (lecturer is not null)
        {
            var detail = string.Join(" - ", new[] { lecturer.AcademicTitle, lecturer.Department }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return new SocialUserProfileData(user.Id, user.DisplayName, lecturer.AvatarUrl, RoleNames.Lecturer, detail);
        }

        var company = await dbContext.CompanyMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new { CompanyName = m.Company.Name, LogoUrl = m.Company.LogoUrl })
            .FirstOrDefaultAsync(cancellationToken);
        if (company is not null)
        {
            return new SocialUserProfileData(user.Id, user.DisplayName, company.LogoUrl, RoleNames.Company, company.CompanyName);
        }

        return new SocialUserProfileData(user.Id, user.DisplayName, null, RoleNames.Student, null);
    }

    public async Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    // ---------- Core helpers ----------

    private async Task<(Guid UserId, string DisplayName, string? AvatarUrl, string Role, string? Detail)?> GetUserBasicsCoreAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null) return null;

        var student = await dbContext.StudentProfiles.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.StudentCode, s.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);
        if (student is not null)
        {
            return (user.Id, user.DisplayName, student.AvatarUrl, RoleNames.Student, student.StudentCode);
        }

        var lecturer = await dbContext.LecturerProfiles.AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => new { l.AcademicTitle, l.Department, l.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);
        if (lecturer is not null)
        {
            var detail = string.Join(" - ", new[] { lecturer.AcademicTitle, lecturer.Department }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return (user.Id, user.DisplayName, lecturer.AvatarUrl, RoleNames.Lecturer, detail);
        }

        var company = await dbContext.CompanyMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new { CompanyName = m.Company.Name, LogoUrl = m.Company.LogoUrl })
            .FirstOrDefaultAsync(cancellationToken);
        if (company is not null)
        {
            return (user.Id, user.DisplayName, company.LogoUrl, RoleNames.Company, company.CompanyName);
        }

        return (user.Id, user.DisplayName, null, RoleNames.Student, null);
    }

    private async Task<string> ResolveRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await dbContext.StudentProfiles.AsNoTracking().AnyAsync(s => s.UserId == userId, cancellationToken)) return RoleNames.Student;
        if (await dbContext.LecturerProfiles.AsNoTracking().AnyAsync(l => l.UserId == userId, cancellationToken)) return RoleNames.Lecturer;
        if (await dbContext.CompanyMembers.AsNoTracking().AnyAsync(m => m.UserId == userId, cancellationToken)) return RoleNames.Company;
        return RoleNames.Student;
    }

    private async Task<List<Guid>> StudentSkillIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await dbContext.StudentProfiles.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (studentId is null) return [];

        return await dbContext.StudentSkills.AsNoTracking()
            .Where(ss => ss.StudentId == studentId.Value)
            .Select(ss => ss.SkillId)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> CompanyProjectSkillIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.ProjectSkills.AsNoTracking()
            .Where(ps => ps.Project.Company.Members.Any(m => m.UserId == userId))
            .Select(ps => ps.SkillId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> LecturerProjectSkillIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var lecturerId = await dbContext.LecturerProfiles.AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (lecturerId is null) return [];

        return await dbContext.ProjectSkills.AsNoTracking()
            .Where(ps => dbContext.LecturerAssignments.AsNoTracking().Any(a => a.ProjectId == ps.ProjectId
                && a.Status == LecturerAssignmentStatus.ACTIVE
                && a.LecturerId == lecturerId.Value))
            .Select(ps => ps.SkillId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
