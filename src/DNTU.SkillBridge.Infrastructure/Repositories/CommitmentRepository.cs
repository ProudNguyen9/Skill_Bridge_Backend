using DNTU.SkillBridge.Application.Commitments;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the commitment data operations.</summary>
public sealed class CommitmentRepository(AppDbContext dbContext) : ICommitmentRepository
{
    public Task<Guid?> FindStudentIdAsync(Guid userId, CancellationToken cancellationToken) => dbContext.StudentProfiles.AsNoTracking().Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<CommitmentResponse>> ListCommitmentsAsync(Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.ProjectCommitments.AsNoTracking().Where(item => item.StudentId == studentId)
            .OrderByDescending(item => item.CreatedAt).Select(item => new CommitmentResponse(item.Id, item.ProjectId, item.StudentId, item.Status, item.PolicyVersion, item.ConfirmationDeadline, item.ConfirmedAt)).ToListAsync(cancellationToken);

    public Task<CommitmentResponse?> FindCommitmentAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) => dbContext.ProjectCommitments.AsNoTracking().Where(item => item.ProjectId == projectId && item.StudentId == studentId).Select(item => new CommitmentResponse(item.Id, item.ProjectId, item.StudentId, item.Status, item.PolicyVersion, item.ConfirmationDeadline, item.ConfirmedAt)).SingleOrDefaultAsync(cancellationToken);

    public Task<ProjectCommitment?> FindCommitmentForUpdateAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) => dbContext.ProjectCommitments.AsTracking().SingleOrDefaultAsync(item => item.ProjectId == projectId && item.StudentId == studentId, cancellationToken);

    public Task<bool> HasProjectMembershipAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AnyAsync(item => item.ProjectId == projectId && item.StudentId == studentId, cancellationToken);

    public void AddProjectMember(ProjectMember member) => dbContext.ProjectMembers.Add(member);

    public void AddProjectActivity(ProjectActivity activity) => dbContext.ProjectActivities.Add(activity);

    public Task<ProjectMember?> FindActiveMembershipAsync(Guid studentId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AsNoTracking().SingleOrDefaultAsync(item => item.StudentId == studentId && item.IsActive && item.ProjectId == projectId, cancellationToken);

    public Task<bool> HasOpenWithdrawalAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.WithdrawalRequests.AnyAsync(item => item.ProjectId == projectId && item.StudentId == studentId && (item.Status == WithdrawalStatus.REQUESTED || item.Status == WithdrawalStatus.RECOMMENDED), cancellationToken);

    public void AddWithdrawalRequest(WithdrawalRequest withdrawal) => dbContext.WithdrawalRequests.Add(withdrawal);

    public async Task<IReadOnlyCollection<WithdrawalResponse>> ListWithdrawalsAsync(Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.WithdrawalRequests.AsNoTracking().Where(item => item.StudentId == studentId)
            .OrderByDescending(item => item.CreatedAt).Select(item => new WithdrawalResponse(item.Id, item.ProjectId, item.StudentId, item.Reason, item.Status, item.RecommendedByLecturerId, item.RecommendedAt, item.LecturerNote, item.DecidedByUserId, item.DecidedAt, item.DecisionNote, item.CreatedAt)).ToListAsync(cancellationToken);

    public Task<WithdrawalResponse?> FindWithdrawalAsync(Guid studentId, Guid withdrawalId, CancellationToken cancellationToken) =>
        dbContext.WithdrawalRequests.AsNoTracking().Where(item => item.Id == withdrawalId && item.StudentId == studentId)
            .Select(item => new WithdrawalResponse(item.Id, item.ProjectId, item.StudentId, item.Reason, item.Status, item.RecommendedByLecturerId, item.RecommendedAt, item.LecturerNote, item.DecidedByUserId, item.DecidedAt, item.DecisionNote, item.CreatedAt)).SingleOrDefaultAsync(cancellationToken);

    public Task<Guid?> FindLecturerIdAsync(Guid lecturerUserId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles.AsNoTracking().Where(item => item.UserId == lecturerUserId && item.IsActive).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);

    public Task<WithdrawalRequest?> FindWithdrawalForUpdateAsync(Guid withdrawalId, CancellationToken cancellationToken) =>
        dbContext.WithdrawalRequests.AsTracking().SingleOrDefaultAsync(item => item.Id == withdrawalId, cancellationToken);

    public Task<bool> IsLecturerAssignedAsync(Guid projectId, Guid lecturerId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments.AnyAsync(item => item.ProjectId == projectId && item.LecturerId == lecturerId && item.Status == DNTU.SkillBridge.Domain.Lecturers.LecturerAssignmentStatus.ACTIVE, cancellationToken);

    public async Task DeactivateActiveMembershipsAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers
            .Where(item => item.ProjectId == projectId && item.StudentId == studentId && item.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, false), cancellationToken);

    public Task<List<ProjectCommitment>> FindPendingCommitmentsAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.ProjectCommitments.AsTracking()
            .Where(item => item.Status == CommitmentStatus.PENDING && item.ConfirmationDeadline <= now)
            .ToListAsync(cancellationToken);
}
