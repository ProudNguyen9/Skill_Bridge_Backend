using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Commitments;

public interface ICommitmentService
{
    Task<CommitmentResponse?> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CommitmentResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<CommitmentResponse?> ConfirmAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> CreateWithdrawalAsync(Guid userId, CreateWithdrawalRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WithdrawalResponse>> ListMyWithdrawalsAsync(Guid userId, CancellationToken cancellationToken);

    Task<WithdrawalResponse?> GetMyWithdrawalAsync(Guid userId, Guid withdrawalId, CancellationToken cancellationToken);

    Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> RecommendAsync(Guid lecturerUserId, Guid withdrawalId, WithdrawalDecisionRequest request, CancellationToken cancellationToken);

    Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> DecideAsync(Guid adminUserId, Guid withdrawalId, bool approve, WithdrawalDecisionRequest request, CancellationToken cancellationToken);

    Task<int> ExpirePendingCommitmentsAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
