namespace DNTU.SkillBridge.Domain.Lecturers;

/// <summary>
/// Trạng thái duyệt hồ sơ giảng viên: tự đăng ký cần admin duyệt trước khi dùng portal.
/// </summary>
public enum LecturerApprovalStatus
{
    PENDING = 1,
    APPROVED = 2,
    REJECTED = 3
}

/// <summary>
/// Academic profile of a DNTU lecturer. Self-registered lecturers stay PENDING until an admin
/// approves them; admin-provisioned/Google staff profiles are APPROVED at creation.
/// Supervision is only valid while the profile is active and approved.
/// </summary>
public sealed class LecturerProfile : Common.AuditableEntity
{
    public Guid UserId { get; private set; }
    public string? LecturerCode { get; private set; }
    public string? Department { get; private set; }
    public string? AcademicTitle { get; private set; }
    public string? Bio { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public string? OfficeLocation { get; private set; }
    public string? PhoneNumber { get; private set; }
    public bool IsProfilePublic { get; private set; }
    public bool ShowContactInfo { get; private set; }
    public bool IsActive { get; private set; } = true;
    public LecturerApprovalStatus ApprovalStatus { get; private set; } = LecturerApprovalStatus.PENDING;
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewNote { get; private set; }
    public ICollection<LecturerAssignment> Assignments { get; } = [];

    private LecturerProfile() { }

    public LecturerProfile(Guid userId)
    {
        UserId = userId;
    }

    /// <summary>Applies the editable profile fields, including the public name/contact policy.</summary>
    public void Update(
        string? lecturerCode,
        string? department,
        string? academicTitle,
        string? bio,
        string? websiteUrl,
        string? officeLocation,
        string? phoneNumber,
        bool isProfilePublic,
        bool showContactInfo)
    {
        LecturerCode = NullIfEmpty(lecturerCode);
        Department = NullIfEmpty(department);
        AcademicTitle = NullIfEmpty(academicTitle);
        Bio = NullIfEmpty(bio);
        WebsiteUrl = NullIfEmpty(websiteUrl);
        OfficeLocation = NullIfEmpty(officeLocation);
        PhoneNumber = NullIfEmpty(phoneNumber);
        IsProfilePublic = isProfilePublic;
        ShowContactInfo = showContactInfo;
    }

    public void Activate() => IsActive = true;

    public void Suspend() => IsActive = false;

    /// <summary>Admin duyệt hồ sơ giảng viên tự đăng ký.</summary>
    public void Approve(DateTimeOffset at)
    {
        if (ApprovalStatus == LecturerApprovalStatus.APPROVED)
        {
            return;
        }

        ApprovalStatus = LecturerApprovalStatus.APPROVED;
        ReviewedAt = at;
        ReviewNote = null;
        IsActive = true;
    }

    /// <summary>Admin từ chối hồ sơ giảng viên tự đăng ký kèm lý do.</summary>
    public void Reject(DateTimeOffset at, string? note)
    {
        ApprovalStatus = LecturerApprovalStatus.REJECTED;
        ReviewedAt = at;
        ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>Creates an INVITED supervision assignment; the lecturer accepts it to make it ACTIVE.</summary>
    public LecturerAssignment AssignProject(Guid projectId, LecturerAssignmentRole role, string? note)
    {
        var assignment = new LecturerAssignment(Id, projectId, role, note);
        Assignments.Add(assignment);
        return assignment;
    }

    /// <summary>Only active, approved lecturers may take (or keep) supervision assignments.</summary>
    public bool CanTakeAssignments() => IsActive && ApprovalStatus == LecturerApprovalStatus.APPROVED;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Project supervision assignment (the spec's project-lecturer assignment). Projects arrive with Task 12,
/// so ProjectId is a plain reference guarded by a unique index: one supervising lecturer per project for now.
/// </summary>
public sealed class LecturerAssignment : Common.AuditableEntity
{
    public Guid LecturerId { get; private set; }
    public Guid ProjectId { get; private set; }
    public LecturerAssignmentRole Role { get; private set; }
    public LecturerAssignmentStatus Status { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public string? Note { get; private set; }

    private LecturerAssignment() { }

    public LecturerAssignment(Guid lecturerId, Guid projectId, LecturerAssignmentRole role, string? note)
    {
        LecturerId = lecturerId;
        ProjectId = projectId;
        Role = role;
        Status = LecturerAssignmentStatus.INVITED;
        AssignedAt = DateTimeOffset.UtcNow;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>The assigned lecturer accepts the invitation; already handled transitions stay untouched.</summary>
    public void Accept(DateTimeOffset at)
    {
        if (Status != LecturerAssignmentStatus.INVITED)
        {
            return;
        }

        Status = LecturerAssignmentStatus.ACTIVE;
        AcceptedAt = at;
    }

    /// <summary>Ends the supervision period; the note is appended for later auditability (Task 38).</summary>
    public void End(DateTimeOffset at, string? note = null)
    {
        if (Status == LecturerAssignmentStatus.ENDED)
        {
            return;
        }

        Status = LecturerAssignmentStatus.ENDED;
        EndedAt = at;
        if (!string.IsNullOrWhiteSpace(note))
        {
            Note = note.Trim();
        }
    }

    /// <summary>True while the assignment counts as the project's active supervision.</summary>
    public bool IsCurrentlyActive() => Status == LecturerAssignmentStatus.ACTIVE;
}
