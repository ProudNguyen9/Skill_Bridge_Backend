using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Projects;

namespace DNTU.SkillBridge.Application.Projects;

public sealed class ProjectApprovalService(IProjectRepository projectRepository) : IProjectApprovalService
{
    public Task<PagedResponse<AdminApprovalListItemResponse>> ListApprovalsAsync(string? status, PageQuery query, CancellationToken cancellationToken) =>
        projectRepository.ListApprovalsAsync(status, query, cancellationToken);

    public Task<AdminApprovalDetailResponse?> GetApprovalDetailAsync(Guid projectId, CancellationToken cancellationToken) =>
        projectRepository.GetApprovalDetailAsync(projectId, cancellationToken);

    public Task<(AdminDecisionOutcome Outcome, AdminApprovalDetailResponse? Detail)> DecideAsync(Guid projectId, Guid adminUserId, ProjectDecision decision, string? note, CancellationToken cancellationToken) =>
        projectRepository.DecideAsync(projectId, adminUserId, decision, note, cancellationToken);
}
