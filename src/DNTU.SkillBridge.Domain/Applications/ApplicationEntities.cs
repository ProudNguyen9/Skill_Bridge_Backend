using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Applications;

/// <summary>
/// One student's (or team's) application to a project. Uniqueness (ProjectId, StudentId) is enforced
/// by a database index — a student may hold at most one direct application per project.
/// </summary>
public sealed class Application : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public DNTU.SkillBridge.Domain.Projects.Project Project { get; private set; } = null!;

    /// <summary>References StudentProfile.Id (not the user id). For team applications, this is the team leader.</summary>
    public Guid StudentId { get; private set; }
    public DNTU.SkillBridge.Domain.Students.StudentProfile Student { get; private set; } = null!;

    public ApplicationType ApplicationType { get; private set; } = ApplicationType.INDIVIDUAL;

    /// <summary>
    /// Optional student team nominated for this application. Individual applications
    /// remain supported; a nominated team is frozen when the application is accepted.
    /// </summary>
    public Guid? TeamId { get; private set; }
    public DNTU.SkillBridge.Domain.Teams.Team? Team { get; private set; }

    public ApplicationStatus Status { get; private set; } = ApplicationStatus.PENDING;
    public string? CoverLetter { get; private set; }
    public string? GithubUrl { get; private set; }
    public string? PortfolioUrl { get; private set; }
    public string? CvUrl { get; private set; }
    public string? CvFileName { get; private set; }
    public string? Availability { get; private set; }

    /// <summary>Set when the application originated from a lecturer's team recommendation (Bước 24-26).</summary>
    public Guid? RecommendedByLecturerId { get; private set; }

    /// <summary>Conflict resolution preference when student is in both individual & team applications (Bước 18).</summary>
    public bool? IsPreferredInConflict { get; private set; }
    public int? PriorityOrder { get; private set; }
    public string? ConflictNote { get; private set; }

    /// <summary>Set when the student/leader withdraws the application.</summary>
    public DateTimeOffset? WithdrawnAt { get; private set; }
    public Guid? WithdrawnByUserId { get; private set; }
    public string? WithdrawReason { get; private set; }

    /// <summary>Company decision audit fields.</summary>
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public string? DecisionReason { get; private set; }
    public string? InterviewScheduleNote { get; private set; }
    public string? AdjustmentProposalJson { get; private set; }
    public string? CompanyOfferConditions { get; private set; }
    public int? ConfirmationDeadlineDays { get; private set; }

    /// <summary>Set when company proposes adding an individual applicant to an existing accepted team (requires leader approval).</summary>
    public Guid? JoinTargetTeamId { get; private set; }
    public string? JoinProposedRole { get; private set; }
    public Guid? JoinRequestedByUserId { get; private set; }
    public DateTimeOffset? JoinRequestedAt { get; private set; }

    public ICollection<ApplicationMember> Members { get; } = [];

    private Application() { }

    public Application(Guid projectId, Guid studentId, string? coverLetter, Guid? teamId = null)
    {
        ProjectId = projectId;
        StudentId = studentId;
        TeamId = teamId;
        ApplicationType = teamId.HasValue ? ApplicationType.TEAM : ApplicationType.INDIVIDUAL;
        Status = ApplicationStatus.PENDING;
        CoverLetter = string.IsNullOrWhiteSpace(coverLetter) ? null : coverLetter.Trim();
    }

    public void SetAttachments(
        string? githubUrl,
        string? portfolioUrl,
        string? cvUrl,
        string? cvFileName,
        string? availability)
    {
        GithubUrl = Normalize(githubUrl);
        PortfolioUrl = Normalize(portfolioUrl);
        CvUrl = Normalize(cvUrl);
        CvFileName = Normalize(cvFileName);
        Availability = Normalize(availability);
    }

    public void SetRecommendedByLecturer(Guid lecturerId)
    {
        if (lecturerId != Guid.Empty)
        {
            RecommendedByLecturerId = lecturerId;
        }
    }

    public void StartMemberConsent()
    {
        if (!TeamId.HasValue)
        {
            throw new InvalidOperationException("Only team applications require member consent.");
        }

        Status = ApplicationStatus.WAITING_MEMBER_CONSENT;
    }

    public void SubmitAfterMemberConsent()
    {
        if (Status != ApplicationStatus.WAITING_MEMBER_CONSENT)
        {
            throw new InvalidOperationException("Only WAITING_MEMBER_CONSENT applications can be submitted.");
        }

        Status = ApplicationStatus.PENDING;
    }

    public void SetConflictPreference(bool isPreferred, int? priorityOrder, string? note)
    {
        IsPreferredInConflict = isPreferred;
        PriorityOrder = priorityOrder;
        ConflictNote = Normalize(note);
    }

    public void Shortlist(Guid decidedByUserId, DateTimeOffset decidedAt, string? reason, string? interviewScheduleNote = null)
    {
        if (Status is not (ApplicationStatus.PENDING or ApplicationStatus.ADJUSTMENT_REQUESTED))
        {
            throw new InvalidOperationException("Only PENDING applications can be shortlisted.");
        }

        InterviewScheduleNote = Normalize(interviewScheduleNote) ?? InterviewScheduleNote;
        SetCompanyDecision(ApplicationStatus.SHORTLISTED, decidedByUserId, decidedAt, reason);
    }

    public void RequestAdjustment(Guid decidedByUserId, DateTimeOffset decidedAt, string? reason, string? adjustmentProposalJson)
    {
        if (Status is not (ApplicationStatus.PENDING or ApplicationStatus.SHORTLISTED))
        {
            throw new InvalidOperationException("Only active applications can receive an adjustment request.");
        }

        AdjustmentProposalJson = Normalize(adjustmentProposalJson);
        SetCompanyDecision(ApplicationStatus.ADJUSTMENT_REQUESTED, decidedByUserId, decidedAt, reason);
    }

    public void ConfirmAdjustment()
    {
        if (Status != ApplicationStatus.ADJUSTMENT_REQUESTED)
        {
            throw new InvalidOperationException("Only ADJUSTMENT_REQUESTED applications can be confirmed.");
        }

        Status = ApplicationStatus.PENDING;
    }

    public void Reject(Guid decidedByUserId, DateTimeOffset decidedAt, string? reason)
    {
        if (Status is not (ApplicationStatus.PENDING or ApplicationStatus.SHORTLISTED or ApplicationStatus.ADJUSTMENT_REQUESTED or ApplicationStatus.WAITING_LEADER_APPROVAL))
        {
            throw new InvalidOperationException("Only active applications can be rejected.");
        }

        JoinTargetTeamId = null;
        JoinProposedRole = null;
        SetCompanyDecision(ApplicationStatus.REJECTED, decidedByUserId, decidedAt, reason);
    }

    public void AttachToMergedTeam(Guid mergedTeamId)
    {
        if (mergedTeamId != Guid.Empty)
        {
            TeamId = mergedTeamId;
        }
    }

    public void RequestJoinTeam(
        Guid targetTeamId,
        Guid requestedByUserId,
        DateTimeOffset now,
        string? proposedRole,
        string? reason,
        string? conditions,
        int? confirmationDeadlineDays)
    {
        if (ApplicationType != ApplicationType.INDIVIDUAL || TeamId.HasValue)
        {
            throw new InvalidOperationException("Only individual applications can request joining an existing team.");
        }

        if (Status is not (ApplicationStatus.PENDING or ApplicationStatus.SHORTLISTED))
        {
            throw new InvalidOperationException("Only PENDING or SHORTLISTED applications can be proposed to join a team.");
        }

        if (targetTeamId == Guid.Empty || requestedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Target team and actor are required.");
        }

        JoinTargetTeamId = targetTeamId;
        JoinProposedRole = Normalize(proposedRole);
        JoinRequestedByUserId = requestedByUserId;
        JoinRequestedAt = now;
        CompanyOfferConditions = Normalize(conditions);
        ConfirmationDeadlineDays = confirmationDeadlineDays;
        SetCompanyDecision(ApplicationStatus.WAITING_LEADER_APPROVAL, requestedByUserId, now, reason);
    }

    public void ApproveJoinTeam(Guid decidedByUserId, DateTimeOffset now)
    {
        if (Status != ApplicationStatus.WAITING_LEADER_APPROVAL || !JoinTargetTeamId.HasValue)
        {
            throw new InvalidOperationException("Only WAITING_LEADER_APPROVAL applications can be approved by team leader.");
        }

        TeamId = JoinTargetTeamId;
        CompanyOfferConditions ??= null;
        SetCompanyDecision(ApplicationStatus.ACCEPTED, decidedByUserId, now, DecisionReason);
    }

    public void DeclineJoinTeam(string? note)
    {
        if (Status != ApplicationStatus.WAITING_LEADER_APPROVAL)
        {
            throw new InvalidOperationException("Only WAITING_LEADER_APPROVAL applications can be declined by team leader.");
        }

        Status = ApplicationStatus.SHORTLISTED;
        JoinTargetTeamId = null;
        JoinProposedRole = null;
        if (!string.IsNullOrWhiteSpace(note))
        {
            DecisionReason = note.Trim();
        }
    }

    public void Accept(Guid decidedByUserId, DateTimeOffset decidedAt, string? reason, string? conditions = null, int? confirmationDeadlineDays = null)
    {
        if (Status is not (ApplicationStatus.PENDING or ApplicationStatus.SHORTLISTED or ApplicationStatus.ADJUSTMENT_REQUESTED))
        {
            throw new InvalidOperationException("Only active applications can be accepted.");
        }

        CompanyOfferConditions = Normalize(conditions);
        ConfirmationDeadlineDays = confirmationDeadlineDays;
        SetCompanyDecision(ApplicationStatus.ACCEPTED, decidedByUserId, decidedAt, reason);
    }

    /// <summary>
    /// Student-side or team leader withdrawal while not yet accepted/rejected/withdrawn.
    /// </summary>
    public void Withdraw(DateTimeOffset now, string? reason, Guid? withdrawnByUserId = null)
    {
        if (Status is not (ApplicationStatus.PENDING or ApplicationStatus.WAITING_MEMBER_CONSENT or ApplicationStatus.SHORTLISTED or ApplicationStatus.ADJUSTMENT_REQUESTED or ApplicationStatus.WAITING_LEADER_APPROVAL))
        {
            throw new InvalidOperationException("Only active applications can be withdrawn.");
        }

        JoinTargetTeamId = null;
        JoinProposedRole = null;
        Status = ApplicationStatus.WITHDRAWN;
        WithdrawnAt = now;
        WithdrawnByUserId = withdrawnByUserId;
        WithdrawReason = Normalize(reason);
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
        DecisionReason = Normalize(reason);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// A member included in a team application, with proposed role and consent confirmation (Bước 15 & 21).
/// </summary>
public sealed class ApplicationMember : AuditableEntity
{
    public Guid ApplicationId { get; private set; }
    public Application Application { get; private set; } = null!;
    public Guid StudentId { get; private set; }
    public DNTU.SkillBridge.Domain.Students.StudentProfile Student { get; private set; } = null!;
    public string? ProposedRole { get; private set; }
    public MemberConsentStatus ConsentStatus { get; private set; } = MemberConsentStatus.PENDING;
    public bool IsSelectedByCompany { get; private set; } = true;
    public DateTimeOffset? RespondedAt { get; private set; }
    public string? Note { get; private set; }

    private ApplicationMember() { }

    public ApplicationMember(Guid applicationId, Guid studentId, string? proposedRole, MemberConsentStatus consentStatus = MemberConsentStatus.PENDING)
    {
        ApplicationId = applicationId;
        StudentId = studentId;
        ProposedRole = string.IsNullOrWhiteSpace(proposedRole) ? null : proposedRole.Trim();
        ConsentStatus = consentStatus;
        IsSelectedByCompany = true;
        if (consentStatus == MemberConsentStatus.APPROVED)
        {
            RespondedAt = DateTimeOffset.UtcNow;
        }
    }

    public void ApproveConsent(DateTimeOffset now, string? note = null)
    {
        ConsentStatus = MemberConsentStatus.APPROVED;
        RespondedAt = now;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void DeclineConsent(DateTimeOffset now, string? note = null)
    {
        ConsentStatus = MemberConsentStatus.DECLINED;
        RespondedAt = now;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void SetCompanySelection(bool isSelected, string? proposedRole = null)
    {
        IsSelectedByCompany = isSelected;
        if (!string.IsNullOrWhiteSpace(proposedRole))
        {
            ProposedRole = proposedRole.Trim();
        }
    }
}
