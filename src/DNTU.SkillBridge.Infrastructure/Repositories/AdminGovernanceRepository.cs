using DNTU.SkillBridge.Application.Administration;
using DNTU.SkillBridge.Domain.Administration;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the admin governance data operations.</summary>
public sealed class AdminGovernanceRepository(AppDbContext dbContext) : IAdministrationRepository
{
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
                company.Id, company.Name, company.Slug, company.TaxCode,
                company.ContactEmail, company.VerificationStatus.ToString(), company.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<Company?> FindCompanyForUpdateAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Companies.AsTracking().SingleOrDefaultAsync(company => company.Id == companyId, cancellationToken);
}
