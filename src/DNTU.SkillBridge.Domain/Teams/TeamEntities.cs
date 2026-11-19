using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Teams;

/// <summary>A student-owned team that can later be nominated for a project application.</summary>
public sealed class Team : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? AvatarUrl { get; private set; }
    public TeamType TeamType { get; private set; } = TeamType.PROJECT;
    public Guid? TargetProjectId { get; private set; }
    public Guid CreatedByStudentId { get; private set; }
    public bool IsLocked { get; private set; }
    public ICollection<TeamMember> Members { get; } = [];

    private Team() { }

    public Team(
        string name,
        Guid createdByStudentId,
        string? description = null,
        TeamType teamType = TeamType.PROJECT,
        Guid? targetProjectId = null,
        string? leaderFunctionalRole = null,
        string? avatarUrl = null)
    {
        Name = NormalizeName(name);
        Description = NormalizeOptional(description);
        AvatarUrl = NormalizeOptional(avatarUrl);
        TeamType = teamType;
        TargetProjectId = targetProjectId;
        CreatedByStudentId = createdByStudentId;
        Members.Add(new TeamMember(Id, createdByStudentId, TeamMemberRole.LEADER, leaderFunctionalRole ?? "Trưởng nhóm"));
    }

    public void Rename(string name)
    {
        if (IsLocked)
        {
            throw new InvalidOperationException("Locked teams cannot be changed.");
        }

        Name = NormalizeName(name);
    }

    public void UpdateDetails(string name, string? description, TeamType teamType, Guid? targetProjectId, string? avatarUrl = null, bool updateAvatar = false)
    {
        if (IsLocked)
        {
            throw new InvalidOperationException("Locked teams cannot be changed.");
        }

        Name = NormalizeName(name);
        Description = NormalizeOptional(description);
        TeamType = teamType;
        TargetProjectId = targetProjectId;
        if (updateAvatar)
        {
            AvatarUrl = NormalizeOptional(avatarUrl);
        }
    }

    public void UpdateAvatar(string? avatarUrl) => AvatarUrl = NormalizeOptional(avatarUrl);

    public void SetTargetProject(Guid? targetProjectId)
    {
        if (IsLocked)
        {
            throw new InvalidOperationException("Locked teams cannot change their target project.");
        }

        TargetProjectId = targetProjectId;
    }

    public void BindActiveProject(Guid projectId)
    {
        TargetProjectId = projectId;
        IsLocked = true;
    }

    public void ReleaseFromCompletedProject()
    {
        IsLocked = false;
        TargetProjectId = null;
    }

    public void Lock() => IsLocked = true;

    private static string NormalizeName(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A team name is required.", nameof(value)) : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class TeamMember
{
    public Guid TeamId { get; private set; }
    public Team Team { get; private set; } = null!;
    public Guid StudentId { get; private set; }
    public DNTU.SkillBridge.Domain.Students.StudentProfile Student { get; private set; } = null!;
    public TeamMemberRole Role { get; private set; }
    public string? FunctionalRole { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; } = DateTimeOffset.UtcNow;

    private TeamMember() { }

    public TeamMember(Guid teamId, Guid studentId, TeamMemberRole role, string? functionalRole = null)
    {
        TeamId = teamId;
        StudentId = studentId;
        Role = role;
        FunctionalRole = string.IsNullOrWhiteSpace(functionalRole) ? null : functionalRole.Trim();
        JoinedAt = DateTimeOffset.UtcNow;
    }

    public void AssignRole(TeamMemberRole role) => Role = role;

    public void AssignFunctionalRole(string? functionalRole) =>
        FunctionalRole = string.IsNullOrWhiteSpace(functionalRole) ? null : functionalRole.Trim();
}

public sealed class TeamInvitation : AuditableEntity
{
    public Guid TeamId { get; private set; }
    public Team Team { get; private set; } = null!;
    public Guid InvitedStudentId { get; private set; }
    public DNTU.SkillBridge.Domain.Students.StudentProfile InvitedStudent { get; private set; } = null!;
    public Guid InvitedByStudentId { get; private set; }
    public TeamMemberRole Role { get; private set; }
    public string? FunctionalRole { get; private set; }
    public string? Message { get; private set; }
    public TeamInvitationStatus Status { get; private set; } = TeamInvitationStatus.PENDING;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }

    private TeamInvitation() { }

    public TeamInvitation(
        Guid teamId,
        Guid invitedStudentId,
        Guid invitedByStudentId,
        TeamMemberRole role,
        DateTimeOffset expiresAt,
        string? functionalRole = null,
        string? message = null)
    {
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "The invitation expiry must be in the future.");
        }

        TeamId = teamId;
        InvitedStudentId = invitedStudentId;
        InvitedByStudentId = invitedByStudentId;
        Role = role;
        FunctionalRole = string.IsNullOrWhiteSpace(functionalRole) ? null : functionalRole.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        ExpiresAt = expiresAt;
    }

    public bool IsPendingAndUnexpired(DateTimeOffset now) => Status == TeamInvitationStatus.PENDING && ExpiresAt > now;

    public void Accept(DateTimeOffset now)
    {
        EnsurePendingAndUnexpired(now);
        Status = TeamInvitationStatus.ACCEPTED;
        RespondedAt = now;
    }

    public void Reject(DateTimeOffset now)
    {
        EnsurePendingAndUnexpired(now);
        Status = TeamInvitationStatus.REJECTED;
        RespondedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (Status != TeamInvitationStatus.PENDING)
        {
            throw new InvalidOperationException("Only pending invitations can be revoked.");
        }

        Status = TeamInvitationStatus.REVOKED;
        RespondedAt = now;
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status == TeamInvitationStatus.PENDING && ExpiresAt <= now)
        {
            Status = TeamInvitationStatus.EXPIRED;
            RespondedAt = now;
        }
    }

    private void EnsurePendingAndUnexpired(DateTimeOffset now)
    {
        Expire(now);
        if (Status != TeamInvitationStatus.PENDING)
        {
            throw new InvalidOperationException("The invitation is no longer actionable.");
        }
    }
}

/// <summary>Lecturer's recommendation of a student team to a marketplace project (Bước 23–26).</summary>
public sealed class TeamProjectRecommendation : AuditableEntity
{
    public Guid LecturerId { get; private set; }
    public DNTU.SkillBridge.Domain.Lecturers.LecturerProfile Lecturer { get; private set; } = null!;
    public Guid TeamId { get; private set; }
    public Team Team { get; private set; } = null!;
    public Guid ProjectId { get; private set; }
    public DNTU.SkillBridge.Domain.Projects.Project Project { get; private set; } = null!;
    public string Reason { get; private set; } = string.Empty;
    public TeamRecommendationStatus Status { get; private set; } = TeamRecommendationStatus.PENDING;
    public Guid? RespondedByStudentId { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public string? ResponseNote { get; private set; }

    private TeamProjectRecommendation() { }

    public TeamProjectRecommendation(Guid lecturerId, Guid teamId, Guid projectId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A recommendation reason is required.", nameof(reason));
        }

        LecturerId = lecturerId;
        TeamId = teamId;
        ProjectId = projectId;
        Reason = reason.Trim();
        Status = TeamRecommendationStatus.PENDING;
    }

    public void Accept(Guid studentId, DateTimeOffset now, string? note = null)
    {
        if (Status != TeamRecommendationStatus.PENDING)
        {
            throw new InvalidOperationException("Only pending recommendations can be accepted.");
        }

        Status = TeamRecommendationStatus.ACCEPTED;
        RespondedByStudentId = studentId;
        RespondedAt = now;
        ResponseNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void Decline(Guid studentId, DateTimeOffset now, string? note = null)
    {
        if (Status != TeamRecommendationStatus.PENDING)
        {
            throw new InvalidOperationException("Only pending recommendations can be declined.");
        }

        Status = TeamRecommendationStatus.DECLINED;
        RespondedByStudentId = studentId;
        RespondedAt = now;
        ResponseNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
