using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.Application.Submissions;

/// <summary>Data operations for technical submission reviews, their submissions, and outbox events.</summary>
public interface ITechnicalReviewRepository
{
    /// <summary>Reads a submission with its versions with the context's default tracking behavior.</summary>
    Task<ProjectSubmission?> FindSubmissionWithVersionsAsync(Guid submissionId, CancellationToken cancellationToken);

    Task<bool> IsAssignedActiveLecturerAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    void AddReview(TechnicalSubmissionReview review);

    void AddStatusHistory(SubmissionStatusHistory history);

    void AddOutboxMessage(OutboxMessage message);

    /// <summary>Persists pending changes; returns false when the update fails (conflict or persistence error).</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);

    Task<Guid?> FindProjectIdBySubmissionIdAsync(Guid submissionId, CancellationToken cancellationToken);

    Task<bool> IsProjectStudentAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TechnicalReviewResponse>> ListForSubmissionAsync(Guid submissionId, CancellationToken cancellationToken);

    Task<TechnicalReviewLookupRow?> FindReviewRowAsync(Guid reviewId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TechnicalReviewResponse>> ListMineAsync(Guid lecturerUserId, CancellationToken cancellationToken);
}

/// <summary>Projection row pairing a technical review response with its submission id.</summary>
public sealed record TechnicalReviewLookupRow(TechnicalReviewResponse Response, Guid SubmissionId);
