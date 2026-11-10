using DNTU.SkillBridge.Application.Administration;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Administration;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the admin governance data operations.</summary>
public sealed class AdminGovernanceRepository(AppDbContext dbContext) : IAdministrationRepository
{
    public async Task<PagedResponse<AdminUserListItemResponse>> ListUsersAsync(
        AdminUserListQuery query, CancellationToken cancellationToken)
    {
        var users = dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            users = users.Where(user => user.Email.ToLower().Contains(search) || user.DisplayName.ToLower().Contains(search));
        }
        if (query.Role is not null)
            users = users.Where(user => user.UserRoles.Any(membership => membership.Role.NormalizedName == query.Role));
        if (query.IsActive.HasValue)
            users = users.Where(user => user.IsActive == query.IsActive.Value);

        var totalItems = await users.CountAsync(cancellationToken);
        var metadata = PageMetadata.Create(query.Page, query.PageSize, totalItems);
        // Compute in long so even a valid, very large page cannot overflow the SQL offset.
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= totalItems)
            return new PagedResponse<AdminUserListItemResponse>([], metadata);

        var items = await users
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Skip((int)offset)
            .Take(query.PageSize)
            .Select(user => new AdminUserListItemResponse(
                user.Id, user.Email, user.DisplayName,
                user.UserRoles.OrderBy(membership => membership.Role.NormalizedName)
                    .Select(membership => membership.Role.Name).ToArray(),
                user.IsActive, user.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AdminUserListItemResponse>(items, metadata);
    }

    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken) => new(
        await dbContext.Companies.CountAsync(cancellationToken),
        await dbContext.StudentProfiles.CountAsync(cancellationToken),
        await dbContext.LecturerProfiles.CountAsync(cancellationToken),
        await dbContext.Projects.CountAsync(cancellationToken),
        await dbContext.FundingOrders.CountAsync(item => item.Status == Domain.Payments.FundingOrderStatus.PENDING_PAYMENT, cancellationToken),
        await dbContext.Disbursements.CountAsync(item => item.Status == Domain.Payments.DisbursementStatus.ELIGIBLE, cancellationToken));

    public Task<AdminPolicySetting?> FindPolicyAsync(string category, CancellationToken cancellationToken) =>
        dbContext.AdminPolicySettings.AsNoTracking().SingleOrDefaultAsync(item => item.Category == category, cancellationToken);

    public Task<AdminPolicySetting?> FindPolicyForUpdateAsync(string category, CancellationToken cancellationToken) =>
        dbContext.AdminPolicySettings.AsTracking().SingleOrDefaultAsync(item => item.Category == category, cancellationToken);

    public void AddPolicy(AdminPolicySetting policy) => dbContext.AdminPolicySettings.Add(policy);

    public void AddAuditLog(AuditLog auditLog) => dbContext.AuditLogs.Add(auditLog);

    public async Task<PagedResponse<AdminAuditLogListItemResponse>> ListAuditLogsAsync(
        AdminAuditLogListQuery query, CancellationToken cancellationToken)
    {
        var logs = from auditLog in dbContext.AuditLogs.AsNoTracking()
                   join user in dbContext.Users.AsNoTracking() on auditLog.ActorUserId equals user.Id into actorJoin
                   from actor in actorJoin.DefaultIfEmpty()
                   select new { auditLog, actor };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            logs = logs.Where(item =>
                item.auditLog.Action.ToLower().Contains(search) ||
                item.auditLog.EntityType.ToLower().Contains(search) ||
                item.auditLog.EntityId.ToLower().Contains(search) ||
                (item.actor != null && item.actor.DisplayName.ToLower().Contains(search)));
        }

        var totalItems = await logs.CountAsync(cancellationToken);
        var metadata = PageMetadata.Create(query.Page, query.PageSize, totalItems);
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= totalItems)
            return new PagedResponse<AdminAuditLogListItemResponse>([], metadata);

        var items = await logs
            .OrderByDescending(item => item.auditLog.CreatedAt)
            .ThenBy(item => item.auditLog.Id)
            .Skip((int)offset)
            .Take(query.PageSize)
            .Select(item => new AdminAuditLogListItemResponse(
                item.auditLog.Id, item.auditLog.ActorUserId,
                item.actor != null ? item.actor.DisplayName : null,
                item.auditLog.Action, item.auditLog.EntityType, item.auditLog.EntityId,
                item.auditLog.MetadataJson, item.auditLog.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AdminAuditLogListItemResponse>(items, metadata);
    }

    public async Task<IReadOnlyCollection<AdminLecturerReviewItem>> ListLecturersForReviewAsync(string? status, CancellationToken cancellationToken)
    {
        var query = from profile in dbContext.LecturerProfiles.AsNoTracking()
                    join user in dbContext.Users.AsNoTracking() on profile.UserId equals user.Id
                    select new { profile, user };

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<LecturerApprovalStatus>(status.Trim(), ignoreCase: true, out var lecturerStatus))
        {
            query = query.Where(item => item.profile.ApprovalStatus == lecturerStatus);
        }

        return await query
            .OrderByDescending(item => item.profile.CreatedAt)
            .Select(item => new AdminLecturerReviewItem(
                item.profile.Id, item.user.Id, item.user.Email, item.user.DisplayName,
                item.profile.LecturerCode, item.profile.Department,
                item.profile.ApprovalStatus.ToString(), item.profile.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<LecturerProfile?> FindLecturerForUpdateAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles.AsTracking().SingleOrDefaultAsync(profile => profile.Id == lecturerId, cancellationToken);

    public async Task<IReadOnlyCollection<AdminCompanyReviewItem>> ListCompaniesForReviewAsync(string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.Companies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<CompanyVerificationStatus>(status.Trim(), ignoreCase: true, out var companyStatus))
        {
            query = query.Where(company => company.VerificationStatus == companyStatus);
        }

        return await query
            .OrderBy(company => company.Name)
            .Select(company => new AdminCompanyReviewItem(
                company.Id, company.Name, company.Slug, company.LogoUrl, company.TaxCode,
                company.ContactEmail, company.VerificationStatus.ToString(), company.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<Company?> FindCompanyForUpdateAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Companies.AsTracking().SingleOrDefaultAsync(company => company.Id == companyId, cancellationToken);
}
