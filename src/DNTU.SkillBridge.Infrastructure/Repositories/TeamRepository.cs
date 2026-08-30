using DNTU.SkillBridge.Application.Teams;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Domain.Teams;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the team data operations.</summary>
public sealed class TeamRepository(AppDbContext dbContext) : ITeamRepository
{
    public Task<Guid?> FindStudentProfileIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles.AsNoTracking().Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);

    public void AddStudentProfile(StudentProfile profile) => dbContext.StudentProfiles.Add(profile);

    public Task<bool> HasTeamMembershipAsync(Guid teamId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.TeamMembers.AsNoTracking()
            .AnyAsync(member => member.TeamId == teamId && member.StudentId == studentId, cancellationToken);

    public Task<Team?> FindTeamWithMembersAsync(Guid teamId, CancellationToken cancellationToken) =>
        dbContext.Teams.Include(item => item.Members).SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);

    public Task<TeamInvitation?> FindInvitationForStudentAsync(Guid invitationId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.TeamInvitations.Include(item => item.Team).ThenInclude(item => item.Members).SingleOrDefaultAsync(item => item.Id == invitationId && item.InvitedStudentId == studentId, cancellationToken);

    public Task<bool> HasStudentProfileAsync(Guid studentId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles.AsNoTracking().AnyAsync(item => item.Id == studentId, cancellationToken);

    public Task<bool> HasPendingInvitationAsync(Guid teamId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.TeamInvitations.AnyAsync(item => item.TeamId == teamId && item.InvitedStudentId == studentId && item.Status == TeamInvitationStatus.PENDING, cancellationToken);

    public void AddTeam(Team team) => dbContext.Teams.Add(team);

    public void AddTeamInvitation(TeamInvitation invitation) => dbContext.TeamInvitations.Add(invitation);

    public void AddTeamMember(TeamMember member) => dbContext.TeamMembers.Add(member);

    public async Task<List<TeamInvitation>> FindPendingInvitationsAsync(Guid studentId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.TeamInvitations.Where(item => item.InvitedStudentId == studentId && item.Status == TeamInvitationStatus.PENDING && item.ExpiresAt <= now).ToListAsync(cancellationToken);

    public void RemoveTeam(Team team) => dbContext.Teams.Remove(team);

    public void RemoveTeamMember(TeamMember member) => dbContext.TeamMembers.Remove(member);

    public Task<TeamResponse?> ProjectTeamAsync(Guid teamId, CancellationToken cancellationToken) =>
        dbContext.Teams.AsNoTracking().Where(item => item.Id == teamId).Select(item => new TeamResponse(item.Id, item.Name, item.CreatedByStudentId, item.IsLocked, item.Members.OrderBy(member => member.Role).Select(member => new TeamMemberResponse(member.StudentId, member.Role)).ToList(), item.CreatedAt)).SingleOrDefaultAsync(cancellationToken);

    public Task<TeamInvitationResponse?> ProjectInvitationAsync(Guid invitationId, CancellationToken cancellationToken) =>
        dbContext.TeamInvitations.AsNoTracking().Where(item => item.Id == invitationId).Select(item => new TeamInvitationResponse(item.Id, item.TeamId, item.Team.Name, item.InvitedStudentId, item.InvitedByStudentId, item.Role, item.Status, item.ExpiresAt, item.CreatedAt)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TeamInvitationResponse>> ListInvitationsAsync(Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.TeamInvitations.AsNoTracking().Where(item => item.InvitedStudentId == studentId)
            .OrderByDescending(item => item.CreatedAt).Select(item => new TeamInvitationResponse(item.Id, item.TeamId, item.Team.Name, item.InvitedStudentId, item.InvitedByStudentId, item.Role, item.Status, item.ExpiresAt, item.CreatedAt)).ToListAsync(cancellationToken);
}
