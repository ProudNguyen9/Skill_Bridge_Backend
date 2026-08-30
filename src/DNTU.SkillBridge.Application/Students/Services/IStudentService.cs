using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Students;

namespace DNTU.SkillBridge.Application.Students;

public interface IStudentService
{
    Task<StudentProfileResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task<StudentProfileResponse?> UpdateMyProfileAsync(Guid userId, UpdateStudentProfileRequest request, CancellationToken cancellationToken);
    Task<StudentPrivacyResponse> UpdateMyPrivacyAsync(Guid userId, UpdateStudentPrivacyRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DeclaredSkillResponse>> GetMySkillsAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> ReplaceMySkillsAsync(Guid userId, IReadOnlyCollection<DeclaredSkillItem> skills, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CertificateResponse>> GetMyCertificatesAsync(Guid userId, CancellationToken cancellationToken);
    Task<CertificateResponse> AddMyCertificateAsync(Guid userId, UpsertCertificateRequest request, CancellationToken cancellationToken);
    Task<CertificateResponse?> UpdateMyCertificateAsync(Guid userId, Guid certificateId, UpsertCertificateRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteMyCertificateAsync(Guid userId, Guid certificateId, CancellationToken cancellationToken);
    Task<PublicStudentResponse?> GetPublicStudentAsync(Guid studentId, CancellationToken cancellationToken);
}
