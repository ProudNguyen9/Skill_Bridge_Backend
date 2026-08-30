using DNTU.SkillBridge.Domain.Files;
using DNTU.SkillBridge.Domain.Milestones;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Milestones;

/// <summary>Data operations for project milestones, deliverables, approval history, activities, and outbox events.</summary>
public interface IMilestoneRepository
{
    Task<bool> HasMilestoneWithSequenceAsync(Guid projectId, int sequence, CancellationToken cancellationToken);

    Task<bool> HasOtherMilestoneWithSequenceAsync(Guid projectId, int sequence, Guid milestoneId, CancellationToken cancellationToken);

    void AddMilestone(ProjectMilestone milestone);

    void RemoveMilestone(ProjectMilestone milestone);

    Task<Guid?> FindProjectIdByMilestoneIdAsync(Guid milestoneId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> ListMilestoneIdsAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Reads a milestone with the context's default tracking behavior.</summary>
    Task<ProjectMilestone?> FindMilestoneAsync(Guid milestoneId, CancellationToken cancellationToken);

    Task<ProjectMilestone?> FindMilestoneNoTrackingAsync(Guid milestoneId, CancellationToken cancellationToken);

    /// <summary>Reads a deliverable joined with its milestone with the context's default tracking behavior.</summary>
    Task<(MilestoneDeliverable Deliverable, ProjectMilestone Milestone)?> FindDeliverableWithMilestoneAsync(Guid deliverableId, CancellationToken cancellationToken);

    void AddDeliverable(MilestoneDeliverable deliverable);

    void RemoveDeliverable(MilestoneDeliverable deliverable);

    Task<bool> HasDeliverableAsync(Guid milestoneId, CancellationToken cancellationToken);

    /// <summary>True when any deliverable of the milestone has no file id or a file that is not COMPLETED in the project.</summary>
    Task<bool> HasDeliverableWithMissingCompletedFileAsync(Guid milestoneId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> HasCompletedFileAsync(Guid userId, Guid projectId, Guid fileId, CancellationToken cancellationToken);

    // Authorization lookups
    Task<bool> HasActiveMemberForUserAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> IsCompanyMemberAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> IsCompanyManagerAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> IsActiveLecturerAssignedAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MilestoneDeliverableResponse>> ListDeliverableResponsesAsync(Guid milestoneId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MilestoneApprovalHistoryResponse>> ListApprovalHistoryResponsesAsync(Guid milestoneId, CancellationToken cancellationToken);

    void AddApprovalHistory(MilestoneApprovalHistory history);

    void AddProjectActivity(ProjectActivity activity);

    void AddOutboxMessage(OutboxMessage message);

    /// <summary>Updates a milestone's status and version in place, succeeding only when the row still carries the persisted version.</summary>
    Task<int> UpdateStatusAndVersionAsync(Guid milestoneId, Guid persistedVersion, MilestoneStatus status, Guid version, CancellationToken cancellationToken);

    /// <summary>Detaches a milestone instance from the change tracker.</summary>
    void Detach(ProjectMilestone milestone);
}
