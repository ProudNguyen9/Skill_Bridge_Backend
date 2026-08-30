using DNTU.SkillBridge.Domain.Students;

namespace DNTU.SkillBridge.Application.Students;

public interface IStudentRepository
{
    Task<StudentProfile?> FindProfileForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    void AddProfile(StudentProfile profile);

    Task<bool> ValidateMajorFacultyAsync(Guid? majorId, Guid? facultyId, CancellationToken cancellationToken);

    Task<StudentProfileResponse> ProjectProfileAsync(StudentProfile profile, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DeclaredSkillResponse>> ListSkillsAsync(Guid studentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> ListActiveSkillIdsAsync(IReadOnlyCollection<Guid> skillIds, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StudentSkill>> ListSkillsForUpdateAsync(Guid studentId, CancellationToken cancellationToken);

    void AddSkill(StudentSkill skill);

    void RemoveSkill(StudentSkill skill);

    Task<IReadOnlyCollection<CertificateResponse>> ListCertificatesAsync(Guid studentId, CancellationToken cancellationToken);

    void AddCertificate(StudentCertificate certificate);

    Task<StudentCertificate?> FindCertificateForUpdateAsync(Guid studentId, Guid certificateId, CancellationToken cancellationToken);

    void RemoveCertificate(StudentCertificate certificate);

    Task<PublicStudentResponse?> FindPublicStudentAsync(Guid studentId, CancellationToken cancellationToken);
}
