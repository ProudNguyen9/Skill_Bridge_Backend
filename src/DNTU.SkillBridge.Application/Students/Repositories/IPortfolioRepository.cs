using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Domain.Portfolio;

namespace DNTU.SkillBridge.Application.Students;

/// <summary>Data operations for verified skills, portfolio entries, and their projections.</summary>
public interface IPortfolioRepository
{
    Task<IReadOnlyCollection<VerifiedSkillResponse>> ListVerifiedSkillsAsync(Guid studentId, bool includeRevoked, CancellationToken cancellationToken);

    Task<VerifiedSkillResponse?> FindVerifiedSkillResponseAsync(Guid verifiedSkillId, CancellationToken cancellationToken);

    Task<bool> IsAssignedLecturerAsync(Guid lecturerUserId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> HasActiveProjectMemberAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken);

    Task<bool> HasProjectSkillAsync(Guid projectId, Guid skillId, CancellationToken cancellationToken);

    Task<AcademicEvaluation?> FindLatestFinalizedEvaluationAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken);

    Task<bool> HasBusinessAcceptedSubmissionAsync(Guid submissionId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>Reads a verified skill as a tracked entity so mutations are persisted.</summary>
    Task<VerifiedSkill?> FindVerifiedSkillForUpdateAsync(Guid studentId, Guid skillId, Guid projectId, CancellationToken cancellationToken);

    void AddVerifiedSkill(VerifiedSkill verifiedSkill);

    Task<IReadOnlyCollection<PortfolioEntryResponse>> ListPortfolioAsync(Guid studentId, bool publicOnly, CancellationToken cancellationToken);

    Task<PortfolioEntryResponse?> FindPortfolioEntryResponseAsync(Guid portfolioEntryId, CancellationToken cancellationToken);

    Task<Guid?> FindStudentIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> CanPortfolioProjectAsync(Guid studentId, Guid projectId, bool requireCompleted, CancellationToken cancellationToken);

    /// <summary>Reads a portfolio entry as a tracked entity so mutations are persisted.</summary>
    Task<PortfolioEntry?> FindPortfolioEntryForUpdateAsync(Guid studentId, Guid projectId, CancellationToken cancellationToken);

    void AddPortfolioEntry(PortfolioEntry entry);

    Task<bool> IsProfilePublicAsync(Guid studentId, CancellationToken cancellationToken);
}
