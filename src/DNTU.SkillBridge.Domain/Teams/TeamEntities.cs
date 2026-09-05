using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Teams;

/// <summary>A student-owned team that can later be nominated for a project application.</summary>
public sealed class Team : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public Guid CreatedByStudentId { get; private set; }
    public bool IsLocked { get; private set; }
    public ICollection<TeamMember> Members { get; } = [];

    private Team() { }

    public Team(string name, Guid createdByStudentId)
    {
        Name = NormalizeName(name);
        CreatedByStudentId = createdByStudentId;
        Members.Add(new TeamMember(Id, createdByStudentId, TeamMemberRole.LEADER));
    }

    public void Rename(string name)
    {
        if (IsLocked)
        {
            throw new InvalidOperationException("Locked teams cannot be changed.");
        }

        Name = NormalizeName(name);
    }

    public void Lock() => IsLocked = true;

    private static string NormalizeName(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A team name is required.", nameof(value)) : value.Trim();
}

public sealed class TeamMember
{
    public Guid TeamId { get; private set; }
    public Team Team { get; private set; } = null!;
    public Guid StudentId { get; private set; }
    public DNTU.SkillBridge.Domain.Students.StudentProfile Student { get; private set; } = null!;
    public TeamMemberRole Role { get; private set; }

    private TeamMember() { }

    public TeamMember(Guid teamId, Guid studentId, TeamMemberRole role)
    {
        TeamId = teamId;
        StudentId = studentId;
        Role = role;
    }

    public void AssignRole(TeamMemberRole role) => Role = role;
}

public sealed class TeamInvitation : AuditableEntity
{
    public Guid TeamId { get; private set; }
    public Team Team { get; private set; } = null!;
    public Guid InvitedStudentId { get; private set; }
    public DNTU.SkillBridge.Domain.Students.StudentProfile InvitedStudent { get; private set; } = null!;
    public Guid InvitedByStudentId { get; private set; }
    public TeamMemberRole Role { get; private set; }
    public TeamInvitationStatus Status { get; private set; } = TeamInvitationStatus.PENDING;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }

    private TeamInvitation() { }

    public TeamInvitation(Guid teamId, Guid invitedStudentId, Guid invitedByStudentId, TeamMemberRole role, DateTimeOffset expiresAt)
    {
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "The invitation expiry must be in the future.");
        }

        TeamId = teamId;
        InvitedStudentId = invitedStudentId;
        InvitedByStudentId = invitedByStudentId;
        Role = role;
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
