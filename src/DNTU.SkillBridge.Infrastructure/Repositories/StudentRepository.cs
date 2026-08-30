using DNTU.SkillBridge.Application.Students;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

public sealed class StudentRepository(AppDbContext dbContext) : IStudentRepository
{
    public Task<StudentProfile?> FindProfileForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles
            .AsTracking()
            .Include(item => item.Privacy)
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

    public void AddProfile(StudentProfile profile) => dbContext.StudentProfiles.Add(profile);

    public async Task<bool> ValidateMajorFacultyAsync(Guid? majorId, Guid? facultyId, CancellationToken cancellationToken)
    {
        if (facultyId.HasValue)
        {
            var facultyActive = await dbContext.Faculties
                .AnyAsync(faculty => faculty.Id == facultyId && faculty.IsActive, cancellationToken);
            if (!facultyActive)
            {
                return false;
            }
        }

        if (majorId.HasValue)
        {
            var major = await dbContext.Majors
                .SingleOrDefaultAsync(item => item.Id == majorId, cancellationToken);
            if (major is null || !major.IsActive)
            {
                return false;
            }

            if (facultyId.HasValue && major.FacultyId != facultyId)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<StudentProfileResponse> ProjectProfileAsync(StudentProfile profile, CancellationToken cancellationToken)
    {
        string? facultyName = null;
        string? majorName = null;

        if (profile.FacultyId.HasValue)
        {
            facultyName = await dbContext.Faculties
                .Where(faculty => faculty.Id == profile.FacultyId)
                .Select(faculty => faculty.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (profile.MajorId.HasValue)
        {
            majorName = await dbContext.Majors
                .Where(major => major.Id == profile.MajorId)
                .Select(major => major.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new StudentProfileResponse(
            profile.Id,
            profile.StudentCode,
            profile.MajorId,
            majorName,
            profile.FacultyId,
            facultyName,
            profile.AcademicYear,
            profile.PhoneNumber,
            profile.Bio,
            profile.GithubUrl,
            profile.LinkedinUrl,
            profile.PortfolioUrl,
            profile.CvUrl,
            new StudentPrivacyResponse(profile.Privacy.IsProfilePublic, profile.Privacy.ShowContactInfo, profile.Privacy.ShowDeclaredSkills, profile.Privacy.ShowCertificates),
            profile.ComputeCompletionPercent());
    }

    public async Task<IReadOnlyCollection<DeclaredSkillResponse>> ListSkillsAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.StudentSkills
            .AsNoTracking()
            .Where(skill => skill.StudentId == studentId)
            .Join(dbContext.Skills,
                studentSkill => studentSkill.SkillId,
                skill => skill.Id,
                (studentSkill, skill) => new { studentSkill, skill })
            .OrderBy(item => item.skill.Name)
            .Select(item => new { item.studentSkill.Level, item.studentSkill.SkillId, SkillCode = item.skill.Code, SkillName = item.skill.Name, Category = item.skill.Category })
            .ToListAsync(cancellationToken);

        return rows
            .Select(item => new DeclaredSkillResponse(item.SkillId, item.SkillCode, item.SkillName, item.Category.ToString(), item.Level))
            .ToList();
    }

    public async Task<IReadOnlyCollection<Guid>> ListActiveSkillIdsAsync(IReadOnlyCollection<Guid> skillIds, CancellationToken cancellationToken) =>
        await dbContext.Skills
            .Where(skill => skill.IsActive && skillIds.Contains(skill.Id))
            .Select(skill => skill.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<StudentSkill>> ListSkillsForUpdateAsync(Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.StudentSkills
            .AsTracking()
            .Where(skill => skill.StudentId == studentId)
            .ToListAsync(cancellationToken);

    public void AddSkill(StudentSkill skill) => dbContext.StudentSkills.Add(skill);

    public void RemoveSkill(StudentSkill skill) => dbContext.StudentSkills.Remove(skill);

    public async Task<IReadOnlyCollection<CertificateResponse>> ListCertificatesAsync(Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.StudentCertificates
            .AsNoTracking()
            .Where(certificate => certificate.StudentId == studentId)
            .OrderByDescending(certificate => certificate.IssueDate)
            .ThenBy(certificate => certificate.Name)
            .Select(certificate => new CertificateResponse(certificate.Id, certificate.Name, certificate.Issuer, certificate.IssueDate, certificate.CertificateUrl))
            .ToListAsync(cancellationToken);

    public void AddCertificate(StudentCertificate certificate) => dbContext.StudentCertificates.Add(certificate);

    public Task<StudentCertificate?> FindCertificateForUpdateAsync(Guid studentId, Guid certificateId, CancellationToken cancellationToken) =>
        dbContext.StudentCertificates
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == certificateId && item.StudentId == studentId, cancellationToken);

    public void RemoveCertificate(StudentCertificate certificate) => dbContext.StudentCertificates.Remove(certificate);

    public async Task<PublicStudentResponse?> FindPublicStudentAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var row = await dbContext.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == studentId)
            .Join(dbContext.Users,
                profile => profile.UserId,
                user => user.Id,
                (profile, user) => new
                {
                    ProfileId = profile.Id,
                    profile.FacultyId,
                    profile.MajorId,
                    profile.AcademicYear,
                    profile.Bio,
                    profile.GithubUrl,
                    profile.LinkedinUrl,
                    profile.PortfolioUrl,
                    profile.PhoneNumber,
                    IsProfilePublic = profile.Privacy.IsProfilePublic,
                    ShowContactInfo = profile.Privacy.ShowContactInfo,
                    ShowDeclaredSkills = profile.Privacy.ShowDeclaredSkills,
                    ShowCertificates = profile.Privacy.ShowCertificates,
                    DisplayName = user.DisplayName,
                    user.IsActive
                })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null || !row.IsActive || !row.IsProfilePublic)
        {
            return null;
        }

        string? facultyName = null;
        string? majorName = null;
        if (row.FacultyId.HasValue)
        {
            facultyName = await dbContext.Faculties
                .Where(faculty => faculty.Id == row.FacultyId)
                .Select(faculty => faculty.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (row.MajorId.HasValue)
        {
            majorName = await dbContext.Majors
                .Where(major => major.Id == row.MajorId)
                .Select(major => major.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var skills = row.ShowDeclaredSkills
            ? await dbContext.StudentSkills
                .AsNoTracking()
                .Where(studentSkill => studentSkill.StudentId == row.ProfileId)
                .Join(dbContext.Skills,
                    studentSkill => studentSkill.SkillId,
                    skill => skill.Id,
                    (studentSkill, skill) => new { studentSkill, skill })
                .Where(item => item.skill.IsActive)
                .OrderBy(item => item.skill.Name)
                .Select(item => new PublicStudentSkillResponse(item.studentSkill.SkillId, item.skill.Name, item.studentSkill.Level))
                .ToListAsync(cancellationToken)
            : [];

        var certificates = row.ShowCertificates
            ? await dbContext.StudentCertificates
                .AsNoTracking()
                .Where(certificate => certificate.StudentId == row.ProfileId)
                .OrderByDescending(certificate => certificate.IssueDate)
                .Select(certificate => new PublicStudentCertificateResponse(certificate.Name, certificate.Issuer, certificate.IssueDate))
                .ToListAsync(cancellationToken)
            : [];

        return new PublicStudentResponse(
            row.ProfileId,
            row.DisplayName,
            row.FacultyId,
            facultyName,
            row.MajorId,
            majorName,
            row.AcademicYear,
            row.Bio,
            row.GithubUrl,
            row.LinkedinUrl,
            row.PortfolioUrl,
            row.ShowContactInfo ? row.PhoneNumber : null,
            skills,
            certificates);
    }
}
