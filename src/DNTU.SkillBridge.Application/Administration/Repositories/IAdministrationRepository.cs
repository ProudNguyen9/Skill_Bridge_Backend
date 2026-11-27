using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Administration;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;

namespace DNTU.SkillBridge.Application.Administration;

/// <summary>Data operations for the admin governance dashboard and policy settings.</summary>
public interface IAdministrationRepository
{
    Task<PagedResponse<AdminUserListItemResponse>> ListUsersAsync(AdminUserListQuery query, CancellationToken cancellationToken);

    /// <summary>Đọc một user dạng không tracked kèm roles (projection an toàn).</summary>
    Task<AdminUserListItemResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken);

    /// <summary>Reads a policy setting without tracking it.</summary>
    Task<AdminPolicySetting?> FindPolicyAsync(string category, CancellationToken cancellationToken);

    /// <summary>Reads a policy setting as a tracked entity so mutations are persisted.</summary>
    Task<AdminPolicySetting?> FindPolicyForUpdateAsync(string category, CancellationToken cancellationToken);

    void AddPolicy(AdminPolicySetting policy);

    void AddAuditLog(AuditLog auditLog);

    Task<PagedResponse<AdminAuditLogListItemResponse>> ListAuditLogsAsync(AdminAuditLogListQuery query, CancellationToken cancellationToken);

    /// <summary>Danh sách hồ sơ giảng viên theo trạng thái duyệt (null = tất cả).</summary>
    Task<IReadOnlyCollection<AdminLecturerReviewItem>> ListLecturersForReviewAsync(string? status, CancellationToken cancellationToken);

    /// <summary>Đọc hồ sơ giảng viên dạng tracked để duyệt/từ chối.</summary>
    Task<LecturerProfile?> FindLecturerForUpdateAsync(Guid lecturerId, CancellationToken cancellationToken);

    /// <summary>Danh sách công ty theo trạng thái xác minh (null = tất cả).</summary>
    Task<IReadOnlyCollection<AdminCompanyReviewItem>> ListCompaniesForReviewAsync(string? status, CancellationToken cancellationToken);

    /// <summary>Đọc công ty dạng tracked để verify/reject.</summary>
    Task<Company?> FindCompanyForUpdateAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>Đọc user (kèm roles) dạng tracked để khóa/mở/thay đổi vai trò.</summary>
    Task<User?> FindUserForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Nạp các role entity theo tên đã chuẩn hóa (upper).</summary>
    Task<IReadOnlyCollection<Role>> FindRolesByNamesAsync(IReadOnlyCollection<string> normalizedNames, CancellationToken cancellationToken);

    /// <summary>Id các user đang active, giới hạn theo vai trò (rỗng = tất cả).</summary>
    Task<IReadOnlyCollection<Guid>> ListActiveUserIdsByRolesAsync(IReadOnlyCollection<string> normalizedRoleNames, CancellationToken cancellationToken);

    /// <summary>Hồ sơ chi tiết giảng viên kèm tài khoản, phục vụ màn duyệt chi tiết.</summary>
    Task<AdminLecturerDetailResponse?> GetLecturerDetailAsync(Guid lecturerId, CancellationToken cancellationToken);

    /// <summary>Danh sách UserId của các thành viên thuộc một doanh nghiệp.</summary>
    Task<IReadOnlyCollection<Guid>> ListCompanyMemberUserIdsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Guid>>([]);
}
