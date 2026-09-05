namespace DNTU.SkillBridge.Domain.Students;

public sealed class StudentProfile : Common.AuditableEntity
{
    public Guid UserId { get; private set; }
    public string? StudentCode { get; private set; }
    public Guid? MajorId { get; private set; }
    public Guid? FacultyId { get; private set; }
    public string? AcademicYear { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Bio { get; private set; }
    public string? GithubUrl { get; private set; }
    public string? LinkedinUrl { get; private set; }
    public string? PortfolioUrl { get; private set; }
    public string? CvUrl { get; private set; }
    public StudentPrivacySettings Privacy { get; private set; } = null!;

    private StudentProfile() { }

    public StudentProfile(Guid userId)
    {
        UserId = userId;
        Privacy = new StudentPrivacySettings(Id);
    }

    /// <summary>Applies the editable contact and education fields; server-owned audit fields stay untouched.</summary>
    public void Update(
        string? studentCode,
        Guid? majorId,
        Guid? facultyId,
        string? academicYear,
        string? phoneNumber,
        string? bio,
        string? githubUrl,
        string? linkedinUrl,
        string? portfolioUrl,
        string? cvUrl)
    {
        StudentCode = NullIfEmpty(studentCode);
        MajorId = majorId;
        FacultyId = facultyId;
        AcademicYear = NullIfEmpty(academicYear);
        PhoneNumber = NullIfEmpty(phoneNumber);
        Bio = NullIfEmpty(bio);
        GithubUrl = NullIfEmpty(githubUrl);
        LinkedinUrl = NullIfEmpty(linkedinUrl);
        PortfolioUrl = NullIfEmpty(portfolioUrl);
        CvUrl = NullIfEmpty(cvUrl);
    }

    public int ComputeCompletionPercent()
    {
        var filled = 0;
        if (!string.IsNullOrWhiteSpace(StudentCode)) filled++;
        if (MajorId.HasValue) filled++;
        if (FacultyId.HasValue) filled++;
        if (!string.IsNullOrWhiteSpace(AcademicYear)) filled++;
        if (!string.IsNullOrWhiteSpace(PhoneNumber)) filled++;
        if (!string.IsNullOrWhiteSpace(Bio)) filled++;
        if (!string.IsNullOrWhiteSpace(GithubUrl)) filled++;
        if (!string.IsNullOrWhiteSpace(CvUrl)) filled++;
        return filled * 100 / 8;
    }

    /// <summary>The minimum fields required to consider the academic profile usable for applications.</summary>
    public bool IsCoreComplete() =>
        !string.IsNullOrWhiteSpace(StudentCode)
        && MajorId.HasValue
        && FacultyId.HasValue
        && !string.IsNullOrWhiteSpace(AcademicYear)
        && !string.IsNullOrWhiteSpace(PhoneNumber);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class StudentPrivacySettings : Common.AuditableEntity
{
    public Guid StudentProfileId { get; private set; }
    public bool IsProfilePublic { get; private set; }
    public bool ShowContactInfo { get; private set; }
    public bool ShowDeclaredSkills { get; private set; } = true;
    public bool ShowCertificates { get; private set; }

    private StudentPrivacySettings() { }

    public StudentPrivacySettings(Guid studentProfileId)
    {
        StudentProfileId = studentProfileId;
    }

    public void Update(bool isProfilePublic, bool showContactInfo, bool showDeclaredSkills, bool showCertificates)
    {
        IsProfilePublic = isProfilePublic;
        ShowContactInfo = showContactInfo;
        ShowDeclaredSkills = showDeclaredSkills;
        ShowCertificates = showCertificates;
    }
}

/// <summary>A self-declared skill. Lecturer-verified skills are separate entities (Task 33) and never share this table.</summary>
public sealed class StudentSkill : Common.AuditableEntity
{
    public Guid StudentId { get; private set; }
    public Guid SkillId { get; private set; }
    public SkillLevel Level { get; private set; }
    public bool IsSelfDeclared { get; private set; }

    private StudentSkill() { }

    public StudentSkill(Guid studentId, Guid skillId, SkillLevel level)
    {
        StudentId = studentId;
        SkillId = skillId;
        Level = level;
        IsSelfDeclared = true;
    }

    public void SetLevel(SkillLevel level) => Level = level;
}

public sealed class StudentCertificate : Common.AuditableEntity
{
    public Guid StudentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Issuer { get; private set; } = string.Empty;
    public DateOnly IssueDate { get; private set; }
    public string? CertificateUrl { get; private set; }

    private StudentCertificate() { }

    public StudentCertificate(Guid studentId, string name, string issuer, DateOnly issueDate, string? certificateUrl)
    {
        StudentId = studentId;
        Name = name.Trim();
        Issuer = issuer.Trim();
        IssueDate = issueDate;
        CertificateUrl = string.IsNullOrWhiteSpace(certificateUrl) ? null : certificateUrl.Trim();
    }

    public void Update(string name, string issuer, DateOnly issueDate, string? certificateUrl)
    {
        Name = name.Trim();
        Issuer = issuer.Trim();
        IssueDate = issueDate;
        CertificateUrl = string.IsNullOrWhiteSpace(certificateUrl) ? null : certificateUrl.Trim();
    }
}
