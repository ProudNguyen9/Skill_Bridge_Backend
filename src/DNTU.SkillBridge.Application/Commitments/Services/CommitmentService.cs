using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Commitments;

public sealed class CommitmentService(ICommitmentRepository commitmentRepository, IUnitOfWork unitOfWork) : ICommitmentService
{
    public async Task<CommitmentResponse?> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        return studentId is null ? null : await commitmentRepository.FindCommitmentAsync(projectId, studentId.Value, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CommitmentResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return [];
        return await commitmentRepository.ListCommitmentsAsync(studentId.Value, cancellationToken);
    }

    public async Task<CommitmentResponse?> ConfirmAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return null;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var commitment = await commitmentRepository.FindCommitmentForUpdateAsync(projectId, studentId.Value, cancellationToken);
        if (commitment is null) return null;
        try { commitment.Confirm(DateTimeOffset.UtcNow); }
        catch (InvalidOperationException) { return null; }

        var membershipExists = await commitmentRepository.HasProjectMembershipAsync(projectId, studentId.Value, cancellationToken);
        if (!membershipExists) commitmentRepository.AddProjectMember(new ProjectMember(projectId, studentId.Value));
        commitmentRepository.AddProjectActivity(new ProjectActivity(projectId, "COMMITMENT_CONFIRMED", userId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await commitmentRepository.FindCommitmentAsync(projectId, studentId.Value, cancellationToken);
    }

    public async Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> CreateWithdrawalAsync(Guid userId, CreateWithdrawalRequest request, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (WithdrawalOutcome.Forbidden, null);
        var member = await commitmentRepository.FindActiveMembershipAsync(studentId.Value, request.ProjectId, cancellationToken);
        if (member is null) return (WithdrawalOutcome.NotFound, null);
        if (await commitmentRepository.HasOpenWithdrawalAsync(member.ProjectId, studentId.Value, cancellationToken)) return (WithdrawalOutcome.Conflict, null);

        var withdrawal = new WithdrawalRequest(member.ProjectId, studentId.Value, request.Reason);
        commitmentRepository.AddWithdrawalRequest(withdrawal);
        commitmentRepository.AddProjectActivity(new ProjectActivity(member.ProjectId, "WITHDRAWAL_REQUESTED", userId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (WithdrawalOutcome.Created, ToResponse(withdrawal));
    }

    public async Task<IReadOnlyCollection<WithdrawalResponse>> ListMyWithdrawalsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return [];
        return await commitmentRepository.ListWithdrawalsAsync(studentId.Value, cancellationToken);
    }

    public async Task<WithdrawalResponse?> GetMyWithdrawalAsync(Guid userId, Guid withdrawalId, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        return studentId is null ? null : await commitmentRepository.FindWithdrawalAsync(studentId.Value, withdrawalId, cancellationToken);
    }

    public async Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> RecommendAsync(Guid lecturerUserId, Guid withdrawalId, WithdrawalDecisionRequest request, CancellationToken cancellationToken)
    {
        var lecturerId = await commitmentRepository.FindLecturerIdAsync(lecturerUserId, cancellationToken);
        if (lecturerId is null) return (WithdrawalOutcome.Forbidden, null);
        var withdrawal = await commitmentRepository.FindWithdrawalForUpdateAsync(withdrawalId, cancellationToken);
        if (withdrawal is null) return (WithdrawalOutcome.NotFound, null);
        var assigned = await commitmentRepository.IsLecturerAssignedAsync(withdrawal.ProjectId, lecturerId.Value, cancellationToken);
        if (!assigned) return (WithdrawalOutcome.Forbidden, null);
        try { withdrawal.Recommend(lecturerId.Value, DateTimeOffset.UtcNow, request.Note); }
        catch (InvalidOperationException) { return (WithdrawalOutcome.Conflict, null); }
        commitmentRepository.AddProjectActivity(new ProjectActivity(withdrawal.ProjectId, "WITHDRAWAL_RECOMMENDED", lecturerUserId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (WithdrawalOutcome.Updated, ToResponse(withdrawal));
    }

    public async Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> DecideAsync(Guid adminUserId, Guid withdrawalId, bool approve, WithdrawalDecisionRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var withdrawal = await commitmentRepository.FindWithdrawalForUpdateAsync(withdrawalId, cancellationToken);
        if (withdrawal is null) return (WithdrawalOutcome.NotFound, null);
        try { withdrawal.Decide(adminUserId, DateTimeOffset.UtcNow, approve, request.Note); }
        catch (InvalidOperationException) { return (WithdrawalOutcome.Conflict, null); }
        if (approve)
        {
            // Execute directly in the same transaction so withdrawal approval cannot leave
            // a stale active membership behind when the aggregate was loaded elsewhere.
            await commitmentRepository.DeactivateActiveMembershipsAsync(withdrawal.ProjectId, withdrawal.StudentId, cancellationToken);
        }
        commitmentRepository.AddProjectActivity(new ProjectActivity(withdrawal.ProjectId, approve ? "WITHDRAWAL_APPROVED" : "WITHDRAWAL_REJECTED", adminUserId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (WithdrawalOutcome.Updated, ToResponse(withdrawal));
    }

    /// <summary>
    /// Job integration point for commitment-expiry processing. It is safe to invoke repeatedly;
    /// only pending commitments past their UTC deadline transition to ABANDONED.
    /// </summary>
    public async Task<int> ExpirePendingCommitmentsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pending = await commitmentRepository.FindPendingCommitmentsAsync(now, cancellationToken);
        if (pending.Count == 0) return 0;
        foreach (var commitment in pending)
        {
            commitment.Expire(now);
            commitmentRepository.AddProjectActivity(new ProjectActivity(commitment.ProjectId, "COMMITMENT_ABANDONED", null));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }

    private static WithdrawalResponse ToResponse(WithdrawalRequest item) => new(item.Id, item.ProjectId, item.StudentId, item.Reason, item.Status, item.RecommendedByLecturerId, item.RecommendedAt, item.LecturerNote, item.DecidedByUserId, item.DecidedAt, item.DecisionNote, item.CreatedAt);
}
