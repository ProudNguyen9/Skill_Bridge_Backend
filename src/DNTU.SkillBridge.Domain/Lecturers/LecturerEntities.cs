namespace DNTU.SkillBridge.Domain.Lecturers;

/// <summary>Enum names intentionally match the uppercase wire values (INVITED, ACTIVE, ENDED).</summary>
public enum LecturerAssignmentStatus
{
    INVITED = 1,
    ACTIVE = 2,
    ENDED = 3
}

/// <summary>The supervision role the lecturer holds on a project (primary supervisor vs. co-supervisor).</summary>
public enum LecturerAssignmentRole
{
    PRIMARY = 1,
    SUPERVISOR = 2
}

/// <summary>
/// Academic profile of a DNTU lecturer. Accounts are provisioned by admins (Task 38), so the row is
/// created lazily on first /lecturers/me access; supervision is only valid while the profile is active.
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

    /// <summary>Creates an INVITED supervision assignment; the lecturer accepts it to make it ACTIVE.</summary>
    public LecturerAssignment AssignProject(Guid projectId, LecturerAssignmentRole role, string? note)
    {
        var assignment = new LecturerAssignment(Id, projectId, role, note);
        Assignments.Add(assignment);
        return assignment;
    }

    /// <summary>Only active lecturers may take (or keep) supervision assignments.</summary>
    public bool CanTakeAssignments() => IsActive;

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
