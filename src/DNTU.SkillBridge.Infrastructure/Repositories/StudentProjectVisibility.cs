using DNTU.SkillBridge.Domain.Applications;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>Shared visibility lookup resolving active and pending-commitment projects for a student.</summary>
internal static class StudentProjectVisibility
{
    public static async Task<IReadOnlyList<Guid>> ListVisibleProjectIdsAsync(
        AppDbContext dbContext,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var activeMemberProjectIds = await dbContext.ProjectMembers.AsNoTracking()
            .Where(member => member.StudentId == studentId && member.IsActive)
            .Select(member => member.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var inactiveMemberProjectIds = await dbContext.ProjectMembers.AsNoTracking()
            .Where(member => member.StudentId == studentId && !member.IsActive)
            .Select(member => member.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var approvedWithdrawalProjectIds = await dbContext.WithdrawalRequests.AsNoTracking()
            .Where(w => w.StudentId == studentId && w.Status == WithdrawalStatus.APPROVED)
            .Select(w => w.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var studentCommitments = await dbContext.ProjectCommitments.AsNoTracking()
            .Where(c => c.StudentId == studentId)
            .Select(c => new { c.ProjectId, c.Status })
            .ToListAsync(cancellationToken);

        var pendingCommitmentProjectIds = studentCommitments
            .Where(c => c.Status == CommitmentStatus.PENDING)
            .Select(c => c.ProjectId);

        var blockedProjectIds = studentCommitments
            .Where(c => c.Status != CommitmentStatus.PENDING && c.Status != CommitmentStatus.CONFIRMED)
            .Select(c => c.ProjectId)
            .Concat(inactiveMemberProjectIds)
            .Concat(approvedWithdrawalProjectIds)
            .Except(activeMemberProjectIds)
            .ToHashSet();

        var myTeamIds = await dbContext.TeamMembers.AsNoTracking()
            .Where(tm => tm.StudentId == studentId)
            .Select(tm => tm.TeamId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var teamBoundProjectIds = new List<Guid>();
        if (myTeamIds.Count > 0)
        {
            var acceptedTeamApps = await dbContext.Applications.AsNoTracking()
                .Where(app => app.TeamId.HasValue
                    && myTeamIds.Contains(app.TeamId.Value)
                    && app.Status == ApplicationStatus.ACCEPTED)
                .Select(app => new
                {
                    TeamId = app.TeamId!.Value,
                    app.ProjectId,
                    HasCommitments = dbContext.ProjectCommitments.Any(c => c.ApplicationId == app.Id)
                })
                .ToListAsync(cancellationToken);

            var legacyAcceptedProjectIds = acceptedTeamApps
                .Where(x => !x.HasCommitments)
                .Select(x => x.ProjectId)
                .Distinct()
                .Where(pid => !blockedProjectIds.Contains(pid))
                .ToList();

            if (legacyAcceptedProjectIds.Count > 0)
            {
                teamBoundProjectIds = await dbContext.Projects.AsNoTracking()
                    .Where(p => legacyAcceptedProjectIds.Contains(p.Id)
                        && p.Status != ProjectStatus.COMPLETED
                        && p.Status != ProjectStatus.CANCELLED
                        && p.Status != ProjectStatus.REJECTED
                        && p.Status != ProjectStatus.SUSPENDED)
                    .Select(p => p.Id)
                    .ToListAsync(cancellationToken);
            }
        }

        return activeMemberProjectIds
            .Concat(pendingCommitmentProjectIds.Where(pid => !blockedProjectIds.Contains(pid)))
            .Concat(teamBoundProjectIds)
            .Distinct()
            .ToList();
    }
}
