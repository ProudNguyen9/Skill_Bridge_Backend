using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Administration;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Lecturers;

namespace DNTU.SkillBridge.Application.Administration;

public enum LecturerReviewOutcome
{
    NotFound,
    AlreadyProcessed,
    Approved,
    Rejected
}

public enum CompanyReviewOutcome
{
    NotFound,
    AlreadyProcessed,
    Verified,
    Rejected
}

public interface IAdminVerificationService
{
    Task<IReadOnlyCollection<AdminLecturerReviewItem>> ListLecturersAsync(string? status, CancellationToken cancellationToken);
    Task<LecturerReviewOutcome> DecideLecturerAsync(Guid actorUserId, Guid lecturerId, bool approve, string? note, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AdminCompanyReviewItem>> ListCompaniesAsync(string? status, CancellationToken cancellationToken);
    Task<CompanyReviewOutcome> DecideCompanyAsync(Guid actorUserId, Guid companyId, bool approve, string? note, CancellationToken cancellationToken);
}

/// <summary>
/// Duyệt hồ sơ giảng viên tự đăng ký và xác minh công ty đối tác.
/// Ghi AuditLog cho mỗi quyết định để phục vụ truy vết.
/// </summary>
public sealed class AdminVerificationService(IAdministrationRepository administrationRepository, IUnitOfWork unitOfWork) : IAdminVerificationService
{
    public Task<IReadOnlyCollection<AdminLecturerReviewItem>> ListLecturersAsync(string? status, CancellationToken cancellationToken) =>
        administrationRepository.ListLecturersForReviewAsync(status, cancellationToken);

    public Task<IReadOnlyCollection<AdminCompanyReviewItem>> ListCompaniesAsync(string? status, CancellationToken cancellationToken) =>
        administrationRepository.ListCompaniesForReviewAsync(status, cancellationToken);

    public async Task<LecturerReviewOutcome> DecideLecturerAsync(Guid actorUserId, Guid lecturerId, bool approve, string? note, CancellationToken cancellationToken)
    {
        var lecturer = await administrationRepository.FindLecturerForUpdateAsync(lecturerId, cancellationToken);
        if (lecturer is null)
        {
            return LecturerReviewOutcome.NotFound;
        }

        var now = DateTimeOffset.UtcNow;
        if (approve)
        {
            if (lecturer.ApprovalStatus == LecturerApprovalStatus.APPROVED)
            {
                return LecturerReviewOutcome.AlreadyProcessed;
            }

            lecturer.Approve(now);
            administrationRepository.AddAuditLog(new AuditLog(actorUserId, AuditActions.LecturerApproved, nameof(LecturerProfile), lecturer.Id.ToString(), null, null, null, null, null, "{}", now));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return LecturerReviewOutcome.Approved;
        }

        if (lecturer.ApprovalStatus == LecturerApprovalStatus.REJECTED)
        {
            return LecturerReviewOutcome.AlreadyProcessed;
        }

        lecturer.Reject(now, note);
        administrationRepository.AddAuditLog(new AuditLog(actorUserId, AuditActions.LecturerRejected, nameof(LecturerProfile), lecturer.Id.ToString(), null, null, null, null, null, $"{{\"note\":{System.Text.Json.JsonSerializer.Serialize(note ?? string.Empty)}}}", now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return LecturerReviewOutcome.Rejected;
    }

    public async Task<CompanyReviewOutcome> DecideCompanyAsync(Guid actorUserId, Guid companyId, bool approve, string? note, CancellationToken cancellationToken)
    {
        var company = await administrationRepository.FindCompanyForUpdateAsync(companyId, cancellationToken);
        if (company is null)
        {
            return CompanyReviewOutcome.NotFound;
        }

        var now = DateTimeOffset.UtcNow;
        if (approve)
        {
            if (company.VerificationStatus != CompanyVerificationStatus.PENDING)
            {
                return CompanyReviewOutcome.AlreadyProcessed;
            }

            company.MarkVerified(now, note);
            administrationRepository.AddAuditLog(new AuditLog(actorUserId, AuditActions.CompanyVerified, nameof(Company), company.Id.ToString(), company.Id, null, null, null, null, "{}", now));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return CompanyReviewOutcome.Verified;
        }

        if (company.VerificationStatus != CompanyVerificationStatus.PENDING)
        {
            return CompanyReviewOutcome.AlreadyProcessed;
        }

        company.MarkRejected(now, note);
        administrationRepository.AddAuditLog(new AuditLog(actorUserId, AuditActions.CompanyRejected, nameof(Company), company.Id.ToString(), company.Id, null, null, null, null, $"{{\"note\":{System.Text.Json.JsonSerializer.Serialize(note ?? string.Empty)}}}", now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CompanyReviewOutcome.Rejected;
    }
}
