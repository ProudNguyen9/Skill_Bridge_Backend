using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Domain.Teams;

namespace DNTU.SkillBridge.Application.Teams;

/// <summary>Data operations for student teams, team members, and team invitations.</summary>
public interface ITeamRepository
{
    Task<Guid?> FindStudentProfileIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<(Guid StudentId, Guid UserId, string DisplayName)?> FindStudentByIdOrIdentifierAsync(Guid? studentId, string? identifier, CancellationToken cancellationToken);

    Task<Guid?> FindStudentUserIdAsync(Guid studentId, CancellationToken cancellationToken);

    void AddStudentProfile(StudentProfile profile);

    Task<bool> HasTeamMembershipAsync(Guid teamId, Guid studentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TeamResponse>> ListTeamsForStudentAsync(Guid studentId, CancellationToken cancellationToken);

    /// <summary>Reads a team with its members loaded, matching the mutation flows' include shape.</summary>
    Task<Team?> FindTeamWithMembersAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>Reads an invitation addressed to a student with its team and team members loaded.</summary>
    Task<TeamInvitation?> FindInvitationForStudentAsync(Guid invitationId, Guid studentId, CancellationToken cancellationToken);

    Task<TeamInvitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken);

    Task<bool> HasStudentProfileAsync(Guid studentId, CancellationToken cancellationToken);

    Task<bool> HasPendingInvitationAsync(Guid teamId, Guid studentId, CancellationToken cancellationToken);

    Task<bool> HasActiveProjectOrApplicationAsync(Guid teamId, Guid? excludeProjectId, CancellationToken cancellationToken);

    void AddTeam(Team team);

    void AddTeamInvitation(TeamInvitation invitation);

    void AddTeamMember(TeamMember member);

    Task<List<TeamInvitation>> FindPendingInvitationsAsync(Guid studentId, DateTimeOffset now, CancellationToken cancellationToken);

    void RemoveTeam(Team team);

    void RemoveTeamMember(TeamMember member);

    Task<TeamResponse?> ProjectTeamAsync(Guid teamId, CancellationToken cancellationToken);

    Task<TeamInvitationResponse?> ProjectInvitationAsync(Guid invitationId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TeamInvitationResponse>> ListInvitationsAsync(Guid studentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TeamInvitationResponse>> ListTeamInvitationsAsync(Guid teamId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TeamProjectRecommendationResponse>> ListRecommendationsForTeamAsync(Guid teamId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TeamProjectRecommendationResponse>> ListRecommendationsForStudentAsync(Guid studentId, CancellationToken cancellationToken);

    Task<TeamProjectRecommendation?> FindRecommendationForUpdateAsync(Guid recommendationId, CancellationToken cancellationToken);

    Task<Guid?> FindLecturerUserIdAsync(Guid lecturerId, CancellationToken cancellationToken);
}
