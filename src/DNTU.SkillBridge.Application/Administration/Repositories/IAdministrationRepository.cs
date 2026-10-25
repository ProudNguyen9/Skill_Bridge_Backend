using DNTU.SkillBridge.Application.Administration;
using DNTU.SkillBridge.Domain.Administration;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Lecturers;

namespace DNTU.SkillBridge.Application.Administration;

/// <summary>Data operations for the admin governance dashboard and policy settings.</summary>
public interface IAdministrationRepository
{
    Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken);

    /// <summary>Reads a policy setting without tracking it.</summary>
    Task<AdminPolicySetting?> FindPolicyAsync(string category, CancellationToken cancellationToken);

    /// <summary>Reads a policy setting as a tracked entity so mutations are persisted.</summary>
    Task<AdminPolicySetting?> FindPolicyForUpdateAsync(string category, CancellationToken cancellationToken);

    void AddPolicy(AdminPolicySetting policy);

    void AddAuditLog(AuditLog auditLog);

    /// <summary>Danh sách hồ sơ giảng viên theo trạng thái duyệt (null = tất cả).</summary>
    Task<IReadOnlyCollection<AdminLecturerReviewItem>> ListLecturersForReviewAsync(string? status, CancellationToken cancellationToken);

    /// <summary>Đọc hồ sơ giảng viên dạng tracked để duyệt/từ chối.</summary>
    Task<LecturerProfile?> FindLecturerForUpdateAsync(Guid lecturerId, CancellationToken cancellationToken);

    /// <summary>Danh sách công ty theo trạng thái xác minh (null = tất cả).</summary>
    Task<IReadOnlyCollection<AdminCompanyReviewItem>> ListCompaniesForReviewAsync(string? status, CancellationToken cancellationToken);

    /// <summary>Đọc công ty dạng tracked để verify/reject.</summary>
    Task<Company?> FindCompanyForUpdateAsync(Guid companyId, CancellationToken cancellationToken);
}
