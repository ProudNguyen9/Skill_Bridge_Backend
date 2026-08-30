using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Commitments;

public enum WithdrawalOutcome
{
    Created,
    NotFound,
    Conflict,
    Forbidden,
    Updated
}

public sealed class CommitmentService(AppDbContext dbContext)
{
    public async Task<CommitmentResponse?> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var studentId = await StudentIdAsync(userId, cancellationToken);
        return studentId is null ? null : await ProjectAsync(projectId, studentId.Value, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CommitmentResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await StudentIdAsync(userId, cancellationToken);
        if (studentId is null) return [];
        return await dbContext.ProjectCommitments.AsNoTracking().Where(item => item.StudentId == studentId.Value)
            .OrderByDescending(item => item.CreatedAt).Select(item => new CommitmentResponse(item.Id, item.ProjectId, item.StudentId, item.Status, item.PolicyVersion, item.ConfirmationDeadline, item.ConfirmedAt)).ToListAsync(cancellationToken);
    }

    public async Task<CommitmentResponse?> ConfirmAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var studentId = await StudentIdAsync(userId, cancellationToken);
        if (studentId is null) return null;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var commitment = await dbContext.ProjectCommitments.SingleOrDefaultAsync(item => item.ProjectId == projectId && item.StudentId == studentId.Value, cancellationToken);
        if (commitment is null) return null;
        try { commitment.Confirm(DateTimeOffset.UtcNow); }
        catch (InvalidOperationException) { return null; }

        var membershipExists = await dbContext.ProjectMembers.AnyAsync(item => item.ProjectId == projectId && item.StudentId == studentId.Value, cancellationToken);
        if (!membershipExists) dbContext.ProjectMembers.Add(new ProjectMember(projectId, studentId.Value));
        dbContext.ProjectActivities.Add(new ProjectActivity(projectId, "COMMITMENT_CONFIRMED", userId));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ProjectAsync(projectId, studentId.Value, cancellationToken);
    }

    public async Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> CreateWithdrawalAsync(Guid userId, CreateWithdrawalRequest request, CancellationToken cancellationToken)
    {
        var studentId = await StudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (WithdrawalOutcome.Forbidden, null);
        var member = await dbContext.ProjectMembers.AsNoTracking().SingleOrDefaultAsync(item => item.StudentId == studentId.Value && item.IsActive && item.ProjectId == request.ProjectId, cancellationToken);
        if (member is null) return (WithdrawalOutcome.NotFound, null);
        if (await dbContext.WithdrawalRequests.AnyAsync(item => item.ProjectId == member.ProjectId && item.StudentId == studentId.Value && (item.Status == WithdrawalStatus.REQUESTED || item.Status == WithdrawalStatus.RECOMMENDED), cancellationToken)) return (WithdrawalOutcome.Conflict, null);

        var withdrawal = new WithdrawalRequest(member.ProjectId, studentId.Value, request.Reason);
        dbContext.WithdrawalRequests.Add(withdrawal);
        dbContext.ProjectActivities.Add(new ProjectActivity(member.ProjectId, "WITHDRAWAL_REQUESTED", userId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return (WithdrawalOutcome.Created, ToResponse(withdrawal));
    }

    public async Task<IReadOnlyCollection<WithdrawalResponse>> ListMyWithdrawalsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await StudentIdAsync(userId, cancellationToken);
        if (studentId is null) return [];
        return await dbContext.WithdrawalRequests.AsNoTracking().Where(item => item.StudentId == studentId.Value)
            .OrderByDescending(item => item.CreatedAt).Select(item => new WithdrawalResponse(item.Id, item.ProjectId, item.StudentId, item.Reason, item.Status, item.RecommendedByLecturerId, item.RecommendedAt, item.LecturerNote, item.DecidedByUserId, item.DecidedAt, item.DecisionNote, item.CreatedAt)).ToListAsync(cancellationToken);
    }

    public async Task<WithdrawalResponse?> GetMyWithdrawalAsync(Guid userId, Guid withdrawalId, CancellationToken cancellationToken)
    {
        var studentId = await StudentIdAsync(userId, cancellationToken);
        return studentId is null ? null : await dbContext.WithdrawalRequests.AsNoTracking().Where(item => item.Id == withdrawalId && item.StudentId == studentId.Value)
            .Select(item => new WithdrawalResponse(item.Id, item.ProjectId, item.StudentId, item.Reason, item.Status, item.RecommendedByLecturerId, item.RecommendedAt, item.LecturerNote, item.DecidedByUserId, item.DecidedAt, item.DecisionNote, item.CreatedAt)).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> RecommendAsync(Guid lecturerUserId, Guid withdrawalId, WithdrawalDecisionRequest request, CancellationToken cancellationToken)
    {
        var lecturerId = await dbContext.LecturerProfiles.AsNoTracking().Where(item => item.UserId == lecturerUserId && item.IsActive).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        if (lecturerId is null) return (WithdrawalOutcome.Forbidden, null);
        var withdrawal = await dbContext.WithdrawalRequests.AsTracking().SingleOrDefaultAsync(item => item.Id == withdrawalId, cancellationToken);
        if (withdrawal is null) return (WithdrawalOutcome.NotFound, null);
        var assigned = await dbContext.LecturerAssignments.AnyAsync(item => item.ProjectId == withdrawal.ProjectId && item.LecturerId == lecturerId.Value && item.Status == DNTU.SkillBridge.Domain.Lecturers.LecturerAssignmentStatus.ACTIVE, cancellationToken);
        if (!assigned) return (WithdrawalOutcome.Forbidden, null);
        try { withdrawal.Recommend(lecturerId.Value, DateTimeOffset.UtcNow, request.Note); }
        catch (InvalidOperationException) { return (WithdrawalOutcome.Conflict, null); }
        dbContext.ProjectActivities.Add(new ProjectActivity(withdrawal.ProjectId, "WITHDRAWAL_RECOMMENDED", lecturerUserId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return (WithdrawalOutcome.Updated, ToResponse(withdrawal));
    }

    public async Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> DecideAsync(Guid adminUserId, Guid withdrawalId, bool approve, WithdrawalDecisionRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var withdrawal = await dbContext.WithdrawalRequests.AsTracking().SingleOrDefaultAsync(item => item.Id == withdrawalId, cancellationToken);
        if (withdrawal is null) return (WithdrawalOutcome.NotFound, null);
        try { withdrawal.Decide(adminUserId, DateTimeOffset.UtcNow, approve, request.Note); }
        catch (InvalidOperationException) { return (WithdrawalOutcome.Conflict, null); }
        if (approve)
        {
            // Execute directly in the same transaction so withdrawal approval cannot leave
            // a stale active membership behind when the aggregate was loaded elsewhere.
            await dbContext.ProjectMembers
                .Where(item => item.ProjectId == withdrawal.ProjectId && item.StudentId == withdrawal.StudentId && item.IsActive)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, false), cancellationToken);
        }
        dbContext.ProjectActivities.Add(new ProjectActivity(withdrawal.ProjectId, approve ? "WITHDRAWAL_APPROVED" : "WITHDRAWAL_REJECTED", adminUserId));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (WithdrawalOutcome.Updated, ToResponse(withdrawal));
    }

    /// <summary>
    /// Job integration point for commitment-expiry processing. It is safe to invoke repeatedly;
    /// only pending commitments past their UTC deadline transition to ABANDONED.
    /// </summary>
    public async Task<int> ExpirePendingCommitmentsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pending = await dbContext.ProjectCommitments
            .Where(item => item.Status == CommitmentStatus.PENDING && item.ConfirmationDeadline <= now)
            .ToListAsync(cancellationToken);
        foreach (var commitment in pending)
        {
            commitment.Expire(now);
            dbContext.ProjectActivities.Add(new ProjectActivity(commitment.ProjectId, "COMMITMENT_ABANDONED", null));
        }

        return pending.Count == 0 ? 0 : await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<Guid?> StudentIdAsync(Guid userId, CancellationToken cancellationToken) => dbContext.StudentProfiles.AsNoTracking().Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
    private Task<CommitmentResponse?> ProjectAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) => dbContext.ProjectCommitments.AsNoTracking().Where(item => item.ProjectId == projectId && item.StudentId == studentId).Select(item => new CommitmentResponse(item.Id, item.ProjectId, item.StudentId, item.Status, item.PolicyVersion, item.ConfirmationDeadline, item.ConfirmedAt)).SingleOrDefaultAsync(cancellationToken);
    private static WithdrawalResponse ToResponse(WithdrawalRequest item) => new(item.Id, item.ProjectId, item.StudentId, item.Reason, item.Status, item.RecommendedByLecturerId, item.RecommendedAt, item.LecturerNote, item.DecidedByUserId, item.DecidedAt, item.DecisionNote, item.CreatedAt);
}
