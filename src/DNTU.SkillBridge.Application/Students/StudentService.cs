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

public sealed class StudentService(IStudentRepository studentRepository, IUnitOfWork unitOfWork) : IStudentService
{
    /// <summary>Returns the caller's own profile, lazily creating it with default privacy settings on first access.</summary>
    public async Task<StudentProfileResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        return await studentRepository.ProjectProfileAsync(profile, cancellationToken);
    }

    /// <summary>Updates the caller's own profile. Major and faculty must exist and stay consistent in the catalog.</summary>
    public async Task<StudentProfileResponse?> UpdateMyProfileAsync(Guid userId, UpdateStudentProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);

        Guid? majorId = request.MajorId;
        Guid? facultyId = request.FacultyId;
        if (majorId.HasValue || facultyId.HasValue)
        {
            var valid = await studentRepository.ValidateMajorFacultyAsync(majorId, facultyId, cancellationToken);
            if (!valid)
            {
                return null;
            }
        }

        profile.Update(
            request.StudentCode,
            majorId,
            facultyId,
            request.AcademicYear,
            request.PhoneNumber,
            request.Bio,
            request.GithubUrl,
            request.LinkedinUrl,
            request.PortfolioUrl,
            request.CvUrl);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await studentRepository.ProjectProfileAsync(profile, cancellationToken);
    }

    public async Task<StudentPrivacyResponse> UpdateMyPrivacyAsync(Guid userId, UpdateStudentPrivacyRequest request, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        profile.Privacy.Update(request.IsProfilePublic, request.ShowContactInfo, request.ShowDeclaredSkills, request.ShowCertificates);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new StudentPrivacyResponse(profile.Privacy.IsProfilePublic, profile.Privacy.ShowContactInfo, profile.Privacy.ShowDeclaredSkills, profile.Privacy.ShowCertificates);
    }

    /// <summary>Lists the caller's self-declared skills with catalog projections.</summary>
    public async Task<IReadOnlyCollection<DeclaredSkillResponse>> GetMySkillsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        return await studentRepository.ListSkillsAsync(profile.Id, cancellationToken);
    }

    /// <summary>Replaces the caller's declared-skill set. Every skill must exist and be active.</summary>
    public async Task<bool> ReplaceMySkillsAsync(Guid userId, IReadOnlyCollection<DeclaredSkillItem> skills, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);

        var requestedIds = skills.Select(item => item.SkillId).Distinct().ToList();
        var existingSkillIds = await studentRepository.ListActiveSkillIdsAsync(requestedIds, cancellationToken);
        if (existingSkillIds.Count != requestedIds.Count)
        {
            return false;
        }

        var current = await studentRepository.ListSkillsForUpdateAsync(profile.Id, cancellationToken);

        foreach (var currentSkill in current.Where(currentSkill => !requestedIds.Contains(currentSkill.SkillId)))
        {
            studentRepository.RemoveSkill(currentSkill);
        }

        foreach (var item in skills.GroupBy(item => item.SkillId).Select(group => group.First()))
        {
            var existing = current.FirstOrDefault(currentSkill => currentSkill.SkillId == item.SkillId);
            if (existing is null)
            {
                studentRepository.AddSkill(new StudentSkill(profile.Id, item.SkillId, item.Level));
            }
            else
            {
                existing.SetLevel(item.Level);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<CertificateResponse>> GetMyCertificatesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        return await studentRepository.ListCertificatesAsync(profile.Id, cancellationToken);
    }

    public async Task<CertificateResponse> AddMyCertificateAsync(Guid userId, UpsertCertificateRequest request, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        var certificate = new StudentCertificate(profile.Id, request.Name, request.Issuer, request.IssueDate, request.CertificateUrl);
        studentRepository.AddCertificate(certificate);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CertificateResponse(certificate.Id, certificate.Name, certificate.Issuer, certificate.IssueDate, certificate.CertificateUrl);
    }

    /// <summary>Updates a certificate owned by the caller; returns null when the caller does not own it.</summary>
    public async Task<CertificateResponse?> UpdateMyCertificateAsync(Guid userId, Guid certificateId, UpsertCertificateRequest request, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        var certificate = await studentRepository.FindCertificateForUpdateAsync(profile.Id, certificateId, cancellationToken);
        if (certificate is null)
        {
            return null;
        }

        certificate.Update(request.Name, request.Issuer, request.IssueDate, request.CertificateUrl);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CertificateResponse(certificate.Id, certificate.Name, certificate.Issuer, certificate.IssueDate, certificate.CertificateUrl);
    }

    /// <summary>Deletes a certificate owned by the caller; returns false when the caller does not own it.</summary>
    public async Task<bool> DeleteMyCertificateAsync(Guid userId, Guid certificateId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        var certificate = await studentRepository.FindCertificateForUpdateAsync(profile.Id, certificateId, cancellationToken);
        if (certificate is null)
        {
            return false;
        }

        studentRepository.RemoveCertificate(certificate);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Public profile projection. Returns null when the student does not exist or keeps the profile private
    /// (public reads answer 404 instead of leaking existence). Private contact, CV, and non-shared
    /// certificates/skills are excluded.
    /// </summary>
    public async Task<PublicStudentResponse?> GetPublicStudentAsync(Guid studentId, CancellationToken cancellationToken)
    {
        return await studentRepository.FindPublicStudentAsync(studentId, cancellationToken);
    }

    private async Task<StudentProfile> EnsureProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Tracked on purpose: the context defaults to NoTracking and /me writes mutate this entity.
        var profile = await studentRepository.FindProfileForUpdateAsync(userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = new StudentProfile(userId);
        studentRepository.AddProfile(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile;
    }
}
