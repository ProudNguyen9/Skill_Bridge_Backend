namespace DNTU.SkillBridge.Application.Administration;

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
    string? TaxCode,
    string? ContactEmail,
    string VerificationStatus,
    DateTimeOffset RegisteredAt);

public sealed record AdminVerificationDecisionRequest(string? Note);
