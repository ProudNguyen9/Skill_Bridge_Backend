using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Projects;

namespace DNTU.SkillBridge.Application.Projects;

public interface IProjectApprovalService
{
    Task<PagedResponse<AdminApprovalListItemResponse>> ListApprovalsAsync(string? status, PageQuery query, CancellationToken cancellationToken);
    Task<AdminApprovalDetailResponse?> GetApprovalDetailAsync(Guid projectId, CancellationToken cancellationToken);
    Task<(AdminDecisionOutcome Outcome, AdminApprovalDetailResponse? Detail)> DecideAsync(Guid projectId, Guid adminUserId, ProjectDecision decision, string? note, CancellationToken cancellationToken);
}
