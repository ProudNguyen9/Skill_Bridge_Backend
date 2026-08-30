using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Commitments;

/// <summary>Data operations for project commitments, withdrawals, and project activities.</summary>
public interface ICommitmentRepository
{
    Task<Guid?> FindStudentIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CommitmentResponse>> ListCommitmentsAsync(Guid studentId, CancellationToken cancellationToken);

    Task<CommitmentResponse?> FindCommitmentAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken);

    /// <summary>Reads a project commitment as a tracked entity so mutations are persisted.</summary>
    Task<ProjectCommitment?> FindCommitmentForUpdateAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken);

    Task<bool> HasProjectMembershipAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken);

    void AddProjectMember(ProjectMember member);

    void AddProjectActivity(ProjectActivity activity);

    Task<ProjectMember?> FindActiveMembershipAsync(Guid studentId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> HasOpenWithdrawalAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken);

    void AddWithdrawalRequest(WithdrawalRequest withdrawal);

    Task<IReadOnlyCollection<WithdrawalResponse>> ListWithdrawalsAsync(Guid studentId, CancellationToken cancellationToken);

    Task<WithdrawalResponse?> FindWithdrawalAsync(Guid studentId, Guid withdrawalId, CancellationToken cancellationToken);

    Task<Guid?> FindLecturerIdAsync(Guid lecturerUserId, CancellationToken cancellationToken);

    /// <summary>Reads a withdrawal request as a tracked entity so mutations are persisted.</summary>
    Task<WithdrawalRequest?> FindWithdrawalForUpdateAsync(Guid withdrawalId, CancellationToken cancellationToken);

    Task<bool> IsLecturerAssignedAsync(Guid projectId, Guid lecturerId, CancellationToken cancellationToken);

    Task DeactivateActiveMembershipsAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken);

    /// <summary>Reads pending past-deadline commitments as tracked entities so mutations are persisted.</summary>
    Task<List<ProjectCommitment>> FindPendingCommitmentsAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
