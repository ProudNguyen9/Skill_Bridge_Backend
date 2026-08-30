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

public sealed class MilestoneService(IMilestoneRepository milestoneRepository, IUnitOfWork unitOfWork) : IMilestoneService
{
    public async Task<MilestoneResponse?> CreateAsync(Guid userId, Guid projectId, CreateMilestoneRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(userId, projectId, cancellationToken) || request.DueAt <= DateTimeOffset.UtcNow ||
            await milestoneRepository.HasMilestoneWithSequenceAsync(projectId, request.Sequence, cancellationToken))
        {
            return null;
        }

        var milestone = new ProjectMilestone(projectId, request.Title, request.Description, request.Sequence, request.DueAt);
        milestoneRepository.AddMilestone(milestone);
        AddEvent(projectId, "MILESTONE_CREATED", userId, milestone.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetResponseAsync(milestone.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MilestoneResponse>?> ListAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!await CanReadAsync(userId, projectId, cancellationToken)) return null;
        var ids = await milestoneRepository.ListMilestoneIdsAsync(projectId, cancellationToken);
        var milestones = new List<MilestoneResponse>(ids.Count);
        foreach (var id in ids)
        {
            var response = await GetResponseAsync(id, cancellationToken);
            if (response is not null) milestones.Add(response);
        }

        return milestones;
    }

    public async Task<MilestoneResponse?> GetAsync(Guid userId, Guid milestoneId, CancellationToken cancellationToken)
    {
        var projectId = await milestoneRepository.FindProjectIdByMilestoneIdAsync(milestoneId, cancellationToken);
        return projectId.HasValue && await CanReadAsync(userId, projectId.Value, cancellationToken) ? await GetResponseAsync(milestoneId, cancellationToken) : null;
    }

    public async Task<MilestoneResponse?> UpdateAsync(Guid userId, Guid milestoneId, UpdateMilestoneRequest request, CancellationToken cancellationToken)
    {
        var milestone = await milestoneRepository.FindMilestoneAsync(milestoneId, cancellationToken);
        if (milestone is null || !await CanManageAsync(userId, milestone.ProjectId, cancellationToken) || request.DueAt <= DateTimeOffset.UtcNow ||
            await milestoneRepository.HasOtherMilestoneWithSequenceAsync(milestone.ProjectId, request.Sequence, milestoneId, cancellationToken))
        {
            return null;
        }

        try { milestone.Update(request.Title, request.Description, request.Sequence, request.DueAt, request.Version); }
        catch (InvalidOperationException) { return null; }
        AddEvent(milestone.ProjectId, "MILESTONE_UPDATED", userId, milestone.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetResponseAsync(milestone.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid milestoneId, Guid version, CancellationToken cancellationToken)
    {
        var milestone = await milestoneRepository.FindMilestoneAsync(milestoneId, cancellationToken);
        if (milestone is null || !await CanManageAsync(userId, milestone.ProjectId, cancellationToken) || milestone.Status != MilestoneStatus.PLANNED || milestone.Version != version)
        {
            return false;
        }

        AddEvent(milestone.ProjectId, "MILESTONE_DELETED", userId, milestone.Id);
        milestoneRepository.RemoveMilestone(milestone);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MilestoneResponse?> TransitionAsync(Guid userId, Guid milestoneId, string action, TransitionMilestoneRequest request, CancellationToken cancellationToken)
    {
        var milestone = await milestoneRepository.FindMilestoneAsync(milestoneId, cancellationToken);
        if (milestone is null || !await CanReadAsync(userId, milestone.ProjectId, cancellationToken)) return null;
        if (action is "approve" or "request-revision")
        {
            if (!await CanReviewAsync(userId, milestone.ProjectId, cancellationToken)) return null;
        }
        else if (action is not ("start" or "submit") || !await CanContributeAsync(userId, milestone.ProjectId, cancellationToken))
        {
            return null;
        }

        var fromStatus = milestone.Status;
        var persistedVersion = milestone.Version;
        try
        {
            switch (action)
            {
                case "start": milestone.Start(request.Version); break;
                case "submit":
                    if (!await milestoneRepository.HasDeliverableAsync(milestone.Id, cancellationToken) ||
                        await milestoneRepository.HasDeliverableWithMissingCompletedFileAsync(milestone.Id, milestone.ProjectId, cancellationToken)) return null;
                    milestone.Submit(request.Version);
                    break;
                case "request-revision": milestone.RequestRevision(request.Version); break;
                case "approve": milestone.Approve(request.Version); break;
                default: return null;
            }
        }
        catch (InvalidOperationException) { return null; }

        var updated = await milestoneRepository.UpdateStatusAndVersionAsync(milestone.Id, persistedVersion, milestone.Status, milestone.Version, cancellationToken);
        if (updated != 1) return null;

        milestoneRepository.Detach(milestone);
        milestoneRepository.AddApprovalHistory(new MilestoneApprovalHistory(milestone.Id, fromStatus, milestone.Status, userId, request.Note));
        AddEvent(milestone.ProjectId, $"MILESTONE_{action.ToUpperInvariant().Replace('-', '_')}", userId, milestone.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(milestone, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MilestoneDeliverableResponse>?> ListDeliverablesAsync(Guid userId, Guid milestoneId, CancellationToken cancellationToken)
    {
        var milestone = await milestoneRepository.FindMilestoneNoTrackingAsync(milestoneId, cancellationToken);
        if (milestone is null || !await CanReadAsync(userId, milestone.ProjectId, cancellationToken)) return null;
        return await milestoneRepository.ListDeliverableResponsesAsync(milestoneId, cancellationToken);
    }

    public async Task<MilestoneDeliverableResponse?> AddDeliverableAsync(Guid userId, Guid milestoneId, CreateMilestoneDeliverableRequest request, CancellationToken cancellationToken)
    {
        var milestone = await milestoneRepository.FindMilestoneAsync(milestoneId, cancellationToken);
        if (milestone is null || milestone.Status == MilestoneStatus.APPROVED || !await CanContributeAsync(userId, milestone.ProjectId, cancellationToken) ||
            !await IsAuthorizedCompletedFileAsync(userId, milestone.ProjectId, request.FileId, cancellationToken)) return null;
        try
        {
            var deliverable = new MilestoneDeliverable(milestoneId, request.Name, request.Criteria, request.FileId);
            milestoneRepository.AddDeliverable(deliverable);
            AddEvent(milestone.ProjectId, "MILESTONE_DELIVERABLE_CREATED", userId, milestone.Id);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new MilestoneDeliverableResponse(deliverable.Id, deliverable.MilestoneId, deliverable.Name, deliverable.Criteria, deliverable.FileId);
        }
        catch (ArgumentException) { return null; }
    }

    public async Task<MilestoneDeliverableResponse?> UpdateDeliverableAsync(Guid userId, Guid deliverableId, UpdateMilestoneDeliverableRequest request, CancellationToken cancellationToken)
    {
        var row = await milestoneRepository.FindDeliverableWithMilestoneAsync(deliverableId, cancellationToken);
        if (row is null || row.Value.Milestone.Status == MilestoneStatus.APPROVED || !await CanContributeAsync(userId, row.Value.Milestone.ProjectId, cancellationToken) ||
            !await IsAuthorizedCompletedFileAsync(userId, row.Value.Milestone.ProjectId, request.FileId, cancellationToken)) return null;
        try { row.Value.Deliverable.Update(request.Name, request.Criteria, request.FileId); }
        catch (ArgumentException) { return null; }
        AddEvent(row.Value.Milestone.ProjectId, "MILESTONE_DELIVERABLE_UPDATED", userId, row.Value.Milestone.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new MilestoneDeliverableResponse(row.Value.Deliverable.Id, row.Value.Deliverable.MilestoneId, row.Value.Deliverable.Name, row.Value.Deliverable.Criteria, row.Value.Deliverable.FileId);
    }

    public async Task<bool> DeleteDeliverableAsync(Guid userId, Guid deliverableId, CancellationToken cancellationToken)
    {
        var row = await milestoneRepository.FindDeliverableWithMilestoneAsync(deliverableId, cancellationToken);
        if (row is null || row.Value.Milestone.Status == MilestoneStatus.APPROVED || !await CanContributeAsync(userId, row.Value.Milestone.ProjectId, cancellationToken)) return false;
        AddEvent(row.Value.Milestone.ProjectId, "MILESTONE_DELIVERABLE_DELETED", userId, row.Value.Milestone.Id);
        milestoneRepository.RemoveDeliverable(row.Value.Deliverable);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<MilestoneResponse?> GetResponseAsync(Guid milestoneId, CancellationToken cancellationToken)
    {
        var milestone = await milestoneRepository.FindMilestoneNoTrackingAsync(milestoneId, cancellationToken);
        return milestone is null ? null : await BuildResponseAsync(milestone, cancellationToken);
    }

    private async Task<MilestoneResponse> BuildResponseAsync(ProjectMilestone milestone, CancellationToken cancellationToken)
    {
        var deliverables = await milestoneRepository.ListDeliverableResponsesAsync(milestone.Id, cancellationToken);
        var history = await milestoneRepository.ListApprovalHistoryResponsesAsync(milestone.Id, cancellationToken);
        return new MilestoneResponse(milestone.Id, milestone.ProjectId, milestone.Title, milestone.Description, milestone.Sequence, milestone.DueAt, milestone.Status,
            milestone.Status != MilestoneStatus.APPROVED && milestone.DueAt < DateTimeOffset.UtcNow, milestone.Version, milestone.CreatedAt, deliverables, history);
    }

    private async Task<bool> IsAuthorizedCompletedFileAsync(Guid userId, Guid projectId, Guid? fileId, CancellationToken cancellationToken) =>
        !fileId.HasValue || await milestoneRepository.HasCompletedFileAsync(userId, projectId, fileId.Value, cancellationToken);

    private async Task<bool> CanReadAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await milestoneRepository.HasActiveMemberForUserAsync(userId, projectId, cancellationToken) ||
        await milestoneRepository.IsCompanyMemberAsync(userId, projectId, cancellationToken) ||
        await milestoneRepository.IsActiveLecturerAssignedAsync(userId, projectId, cancellationToken);

    private async Task<bool> CanContributeAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) => await CanReadAsync(userId, projectId, cancellationToken);

    private async Task<bool> CanManageAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await milestoneRepository.IsCompanyManagerAsync(userId, projectId, cancellationToken) ||
        await milestoneRepository.IsActiveLecturerAssignedAsync(userId, projectId, cancellationToken);

    private Task<bool> CanReviewAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) => CanManageAsync(userId, projectId, cancellationToken);

    private void AddEvent(Guid projectId, string eventType, Guid actorUserId, Guid milestoneId)
    {
        milestoneRepository.AddProjectActivity(new ProjectActivity(projectId, eventType, actorUserId, JsonSerializer.Serialize(new { milestoneId })));
        milestoneRepository.AddOutboxMessage(new OutboxMessage("milestone.transitioned", JsonSerializer.Serialize(new { projectId, milestoneId, eventType, actorUserId })));
    }
}
