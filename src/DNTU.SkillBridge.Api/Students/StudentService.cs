using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Students;

public sealed class StudentService(AppDbContext dbContext)
{
    /// <summary>Returns the caller's own profile, lazily creating it with default privacy settings on first access.</summary>
    public async Task<StudentProfileResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        return await ProjectProfileAsync(dbContext, profile, cancellationToken);
    }

    /// <summary>Updates the caller's own profile. Major and faculty must exist and stay consistent in the catalog.</summary>
    public async Task<StudentProfileResponse?> UpdateMyProfileAsync(Guid userId, UpdateStudentProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);

        Guid? majorId = request.MajorId;
        Guid? facultyId = request.FacultyId;
        if (majorId.HasValue || facultyId.HasValue)
        {
            var valid = await ValidateMajorFacultyAsync(majorId, facultyId, cancellationToken);
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

        await dbContext.SaveChangesAsync(cancellationToken);
        return await ProjectProfileAsync(dbContext, profile, cancellationToken);
    }

    public async Task<StudentPrivacyResponse> UpdateMyPrivacyAsync(Guid userId, UpdateStudentPrivacyRequest request, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        profile.Privacy.Update(request.IsProfilePublic, request.ShowContactInfo, request.ShowDeclaredSkills, request.ShowCertificates);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new StudentPrivacyResponse(profile.Privacy.IsProfilePublic, profile.Privacy.ShowContactInfo, profile.Privacy.ShowDeclaredSkills, profile.Privacy.ShowCertificates);
    }

    /// <summary>Lists the caller's self-declared skills with catalog projections.</summary>
    public async Task<IReadOnlyCollection<DeclaredSkillResponse>> GetMySkillsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        var rows = await dbContext.StudentSkills
            .AsNoTracking()
            .Where(skill => skill.StudentId == profile.Id)
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

    /// <summary>Replaces the caller's declared-skill set. Every skill must exist and be active.</summary>
    public async Task<bool> ReplaceMySkillsAsync(Guid userId, IReadOnlyCollection<DeclaredSkillItem> skills, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);

        var requestedIds = skills.Select(item => item.SkillId).Distinct().ToList();
        var existingSkillIds = await dbContext.Skills
            .Where(skill => skill.IsActive && requestedIds.Contains(skill.Id))
            .Select(skill => skill.Id)
            .ToListAsync(cancellationToken);
        if (existingSkillIds.Count != requestedIds.Count)
        {
            return false;
        }

        var current = await dbContext.StudentSkills
            .AsTracking()
            .Where(skill => skill.StudentId == profile.Id)
            .ToListAsync(cancellationToken);

        foreach (var currentSkill in current.Where(currentSkill => !requestedIds.Contains(currentSkill.SkillId)))
        {
            dbContext.StudentSkills.Remove(currentSkill);
        }

        foreach (var item in skills.GroupBy(item => item.SkillId).Select(group => group.First()))
        {
            var existing = current.FirstOrDefault(currentSkill => currentSkill.SkillId == item.SkillId);
            if (existing is null)
            {
                dbContext.StudentSkills.Add(new StudentSkill(profile.Id, item.SkillId, item.Level));
            }
            else
            {
                existing.SetLevel(item.Level);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<CertificateResponse>> GetMyCertificatesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        return await dbContext.StudentCertificates
            .AsNoTracking()
            .Where(certificate => certificate.StudentId == profile.Id)
            .OrderByDescending(certificate => certificate.IssueDate)
            .ThenBy(certificate => certificate.Name)
            .Select(certificate => new CertificateResponse(certificate.Id, certificate.Name, certificate.Issuer, certificate.IssueDate, certificate.CertificateUrl))
            .ToListAsync(cancellationToken);
    }

    public async Task<CertificateResponse> AddMyCertificateAsync(Guid userId, UpsertCertificateRequest request, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        var certificate = new StudentCertificate(profile.Id, request.Name, request.Issuer, request.IssueDate, request.CertificateUrl);
        dbContext.StudentCertificates.Add(certificate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CertificateResponse(certificate.Id, certificate.Name, certificate.Issuer, certificate.IssueDate, certificate.CertificateUrl);
    }

    /// <summary>Updates a certificate owned by the caller; returns null when the caller does not own it.</summary>
    public async Task<CertificateResponse?> UpdateMyCertificateAsync(Guid userId, Guid certificateId, UpsertCertificateRequest request, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        var certificate = await dbContext.StudentCertificates
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == certificateId && item.StudentId == profile.Id, cancellationToken);
        if (certificate is null)
        {
            return null;
        }

        certificate.Update(request.Name, request.Issuer, request.IssueDate, request.CertificateUrl);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CertificateResponse(certificate.Id, certificate.Name, certificate.Issuer, certificate.IssueDate, certificate.CertificateUrl);
    }

    /// <summary>Deletes a certificate owned by the caller; returns false when the caller does not own it.</summary>
    public async Task<bool> DeleteMyCertificateAsync(Guid userId, Guid certificateId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        var certificate = await dbContext.StudentCertificates
            .SingleOrDefaultAsync(item => item.Id == certificateId && item.StudentId == profile.Id, cancellationToken);
        if (certificate is null)
        {
            return false;
        }

        dbContext.StudentCertificates.Remove(certificate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Public profile projection. Returns null when the student does not exist or keeps the profile private
    /// (public reads answer 404 instead of leaking existence). Private contact, CV, and non-shared
    /// certificates/skills are excluded.
    /// </summary>
    public async Task<PublicStudentResponse?> GetPublicStudentAsync(Guid studentId, CancellationToken cancellationToken)
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

    private async Task<StudentProfile> EnsureProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Tracked on purpose: the context defaults to NoTracking and /me writes mutate this entity.
        var profile = await dbContext.StudentProfiles
            .AsTracking()
            .Include(item => item.Privacy)
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = new StudentProfile(userId);
        dbContext.StudentProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private async Task<bool> ValidateMajorFacultyAsync(Guid? majorId, Guid? facultyId, CancellationToken cancellationToken)
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

    private static async Task<StudentProfileResponse> ProjectProfileAsync(AppDbContext dbContext, StudentProfile profile, CancellationToken cancellationToken)
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
}
