using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Applications;

/// <summary>
/// One student's application to a project. Uniqueness (ProjectId, StudentId) is enforced
/// by a database index — a student may hold at most one application per project.
/// </summary>
public sealed class Application : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public DNTU.SkillBridge.Domain.Projects.Project Project { get; private set; } = null!;

    /// <summary>References StudentProfile.Id (not the user id).</summary>
    public Guid StudentId { get; private set; }
    public DNTU.SkillBridge.Domain.Students.StudentProfile Student { get; private set; } = null!;

    /// <summary>
    /// Optional student team nominated for this application. Individual applications
    /// remain supported; a nominated team is frozen when the application is accepted.
    /// </summary>
    public Guid? TeamId { get; private set; }
    public DNTU.SkillBridge.Domain.Teams.Team? Team { get; private set; }

    public ApplicationStatus Status { get; private set; } = ApplicationStatus.PENDING;
    public string? CoverLetter { get; private set; }

    /// <summary>Set when the student withdraws the application.</summary>
    public DateTimeOffset? WithdrawnAt { get; private set; }
    public string? WithdrawReason { get; private set; }

    /// <summary>Company decision audit fields. They are intentionally retained after later workflow changes.</summary>
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public string? DecisionReason { get; private set; }

    private Application() { }

    public Application(Guid projectId, Guid studentId, string? coverLetter, Guid? teamId = null)
    {
        ProjectId = projectId;
        StudentId = studentId;
        TeamId = teamId;
        Status = ApplicationStatus.PENDING;
        CoverLetter = string.IsNullOrWhiteSpace(coverLetter) ? null : coverLetter.Trim();
    }

    public void Shortlist(Guid decidedByUserId, DateTimeOffset decidedAt, string? reason)
    {
        if (Status != ApplicationStatus.PENDING)
        {
            throw new InvalidOperationException("Only PENDING applications can be shortlisted.");
        }

        SetCompanyDecision(ApplicationStatus.SHORTLISTED, decidedByUserId, decidedAt, reason);
    }

    public void Reject(Guid decidedByUserId, DateTimeOffset decidedAt, string? reason)
    {
        if (Status is not (ApplicationStatus.PENDING or ApplicationStatus.SHORTLISTED))
        {
            throw new InvalidOperationException("Only active applications can be rejected.");
        }

        SetCompanyDecision(ApplicationStatus.REJECTED, decidedByUserId, decidedAt, reason);
    }

    public void Accept(Guid decidedByUserId, DateTimeOffset decidedAt, string? reason)
    {
        if (Status is not (ApplicationStatus.PENDING or ApplicationStatus.SHORTLISTED))
        {
            throw new InvalidOperationException("Only active applications can be accepted.");
        }

        SetCompanyDecision(ApplicationStatus.ACCEPTED, decidedByUserId, decidedAt, reason);
    }

    /// <summary>
    /// Student-side withdrawal. Only a PENDING application can be withdrawn;
    /// later states are controlled by the company review workflow (Task 18).
    /// </summary>
    public void Withdraw(DateTimeOffset now, string? reason)
    {
        if (Status != ApplicationStatus.PENDING)
        {
            throw new InvalidOperationException("Only PENDING applications can be withdrawn.");
        }

        Status = ApplicationStatus.WITHDRAWN;
        WithdrawnAt = now;
        WithdrawReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    private void SetCompanyDecision(ApplicationStatus status, Guid decidedByUserId, DateTimeOffset decidedAt, string? reason)
    {
        if (decidedByUserId == Guid.Empty)
        {
            throw new ArgumentException("A company decision actor is required.", nameof(decidedByUserId));
        }

        Status = status;
        DecidedByUserId = decidedByUserId;
        DecidedAt = decidedAt;
        DecisionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}
