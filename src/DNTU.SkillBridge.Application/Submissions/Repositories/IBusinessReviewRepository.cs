using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.Application.Submissions;

/// <summary>Data operations for business submission reviews, their submissions, and outbox events.</summary>
public interface IBusinessReviewRepository
{
    /// <summary>Reads a submission with its versions as a tracked entity so mutations are persisted.</summary>
    Task<ProjectSubmission?> FindSubmissionForUpdateAsync(Guid submissionId, CancellationToken cancellationToken);

    Task<(Guid CompanyId, bool CompanyIsActive)?> FindProjectCompanyAsync(Guid projectId, CancellationToken cancellationToken);

    Task<bool> CanReviewForCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);

    void AddReview(BusinessSubmissionReview review);

    void AddStatusHistory(SubmissionStatusHistory history);

    void AddOutboxMessage(OutboxMessage message);

    /// <summary>Persists pending changes; returns false when an optimistic-concurrency conflict is detected.</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);

    Task<(Guid ProjectId, Guid CompanyId)?> FindSubmissionScopeAsync(Guid submissionId, CancellationToken cancellationToken);

    Task<bool> CanReadBusinessReviewAsync(Guid userId, Guid projectId, Guid companyId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BusinessReviewResponse>> ListForSubmissionAsync(Guid submissionId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BusinessReviewResponse>> ListMineAsync(Guid userId, CancellationToken cancellationToken);
}
