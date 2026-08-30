using System.Text.Json;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.Application.Submissions;

public interface ITechnicalReviewService
{
    Task<(TechnicalReviewOutcome Outcome, TechnicalReviewResponse? Review)> CreateAsync(Guid reviewerUserId, bool mayOverrideLecturerScope, Guid submissionId, CreateTechnicalReviewRequest request, CancellationToken cancellationToken);

    Task<(TechnicalReviewOutcome Outcome, IReadOnlyCollection<TechnicalReviewResponse>? Reviews)> ListForSubmissionAsync(Guid requesterUserId, bool mayOverrideLecturerScope, Guid submissionId, CancellationToken cancellationToken);

    Task<(TechnicalReviewOutcome Outcome, TechnicalReviewResponse? Review)> GetAsync(Guid requesterUserId, bool mayOverrideLecturerScope, Guid reviewId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TechnicalReviewResponse>> ListMineAsync(Guid lecturerUserId, CancellationToken cancellationToken);
}
