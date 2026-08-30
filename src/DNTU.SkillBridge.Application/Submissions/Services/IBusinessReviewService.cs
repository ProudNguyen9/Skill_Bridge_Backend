using System.Text.Json;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.Application.Submissions;

public interface IBusinessReviewService
{
    Task<(BusinessReviewOutcome Outcome, BusinessReviewResponse? Review)> CreateAsync(Guid reviewerUserId, Guid submissionId, CreateBusinessReviewRequest request, CancellationToken cancellationToken);

    Task<(BusinessReviewOutcome Outcome, IReadOnlyCollection<BusinessReviewResponse>? Reviews)> ListForSubmissionAsync(Guid requesterUserId, Guid submissionId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BusinessReviewResponse>> ListMineAsync(Guid userId, CancellationToken cancellationToken);
}
