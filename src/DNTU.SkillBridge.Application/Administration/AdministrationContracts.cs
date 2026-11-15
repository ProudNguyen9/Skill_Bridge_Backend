using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.Administration;

public sealed class AdminUserListQuery : PageQuery
{
    public const int MaximumSearchLength = 200;

    public string? Search { get; init; }
    public string? Role { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record AdminUserListItemResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string[] Roles,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record AdminDashboardResponse(int Companies, int Students, int Lecturers, int Projects, int PendingFundingOrders, int EligibleDisbursements);
public sealed record AdminPolicyResponse(string Category, string SettingsJson, int Version, DateTimeOffset? UpdatedAt);

public sealed record AdminLecturerReviewItem(
    Guid LecturerId,
    Guid UserId,
    string Email,
    string DisplayName,
    string? LecturerCode,
    string? Department,
    string ApprovalStatus,
    DateTimeOffset RegisteredAt);

public sealed record AdminCompanyReviewItem(
    Guid CompanyId,
    string Name,
    string Slug,
    string? LogoUrl,
    string? TaxCode,
    string? ContactEmail,
    string VerificationStatus,
    DateTimeOffset RegisteredAt);

public sealed record AdminVerificationDecisionRequest(string? Note);

public sealed class AdminAuditLogListQuery : PageQuery
{
    public const int MaximumSearchLength = 200;

    public string? Search { get; init; }

    /// <summary>Filters logs by the actor's role name (case-insensitive, e.g. ADMIN, SUPER_ADMIN, LECTURER).</summary>
    public string? ActorRole { get; init; }
}

/// <summary>One audit-log row. The domain entity carries no success/failure status, so none is reported.</summary>
public sealed record AdminAuditLogListItemResponse(
    Guid Id,
    Guid? ActorUserId,
    string? ActorName,
    string Action,
    string EntityType,
    string EntityId,
    string MetadataJson,
    DateTimeOffset CreatedAt);

/// <summary>Result of an admin account-status or role change.</summary>
public sealed record AdminUserManagementResponse(Guid UserId, bool IsActive, string[] Roles);

/// <summary>Role replacement payload; names are case-insensitive role names (STUDENT, COMPANY, ...).</summary>
public sealed record AdminSetUserRolesRequest(IReadOnlyCollection<string> Roles);

/// <summary>Full lecturer profile for the admin review detail view, including the previous review note.</summary>
public sealed record AdminLecturerDetailResponse(
    Guid LecturerId,
    Guid UserId,
    string Email,
    string DisplayName,
    string? LecturerCode,
    string? Department,
    string? AcademicTitle,
    string? Bio,
    string? WebsiteUrl,
    string? OfficeLocation,
    string? PhoneNumber,
    string ApprovalStatus,
    string? ReviewNote,
    DateTimeOffset? ReviewedAt,
    bool IsActive,
    DateTimeOffset RegisteredAt);

/// <summary>Admin broadcast notification request; empty target roles means every active user.</summary>
public sealed record AdminBroadcastRequest(string Title, string Type, IReadOnlyCollection<string> TargetRoles);

public sealed record AdminBroadcastResponse(int Recipients);

