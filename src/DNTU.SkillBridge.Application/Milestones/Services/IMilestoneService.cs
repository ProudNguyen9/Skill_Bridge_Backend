using System.Text.Json;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Milestones;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Milestones;

public interface IMilestoneService
{
    Task<MilestoneResponse?> CreateAsync(Guid userId, Guid projectId, CreateMilestoneRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MilestoneResponse>?> ListAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<MilestoneResponse?> GetAsync(Guid userId, Guid milestoneId, CancellationToken cancellationToken);

    Task<MilestoneResponse?> UpdateAsync(Guid userId, Guid milestoneId, UpdateMilestoneRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid milestoneId, Guid version, CancellationToken cancellationToken);

    Task<MilestoneResponse?> TransitionAsync(Guid userId, Guid milestoneId, string action, TransitionMilestoneRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MilestoneDeliverableResponse>?> ListDeliverablesAsync(Guid userId, Guid milestoneId, CancellationToken cancellationToken);

    Task<MilestoneDeliverableResponse?> AddDeliverableAsync(Guid userId, Guid milestoneId, CreateMilestoneDeliverableRequest request, CancellationToken cancellationToken);

    Task<MilestoneDeliverableResponse?> UpdateDeliverableAsync(Guid userId, Guid deliverableId, UpdateMilestoneDeliverableRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteDeliverableAsync(Guid userId, Guid deliverableId, CancellationToken cancellationToken);
}
