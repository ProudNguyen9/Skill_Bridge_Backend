using System.Text.Json;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Files;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Milestones;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Milestones;

public sealed class MilestoneService(AppDbContext dbContext)
{
    public async Task<MilestoneResponse?> CreateAsync(Guid userId, Guid projectId, CreateMilestoneRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(userId, projectId, cancellationToken) || request.DueAt <= DateTimeOffset.UtcNow ||
            await dbContext.ProjectMilestones.AnyAsync(item => item.ProjectId == projectId && item.Sequence == request.Sequence, cancellationToken))
        {
            return null;
        }

        var milestone = new ProjectMilestone(projectId, request.Title, request.Description, request.Sequence, request.DueAt);
        dbContext.ProjectMilestones.Add(milestone);
        AddEvent(projectId, "MILESTONE_CREATED", userId, milestone.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetResponseAsync(milestone.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MilestoneResponse>?> ListAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!await CanReadAsync(userId, projectId, cancellationToken)) return null;
        var ids = await dbContext.ProjectMilestones.AsNoTracking().Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.Sequence).ThenBy(item => item.Id).Select(item => item.Id).ToListAsync(cancellationToken);
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
        var projectId = await dbContext.ProjectMilestones.AsNoTracking().Where(item => item.Id == milestoneId).Select(item => (Guid?)item.ProjectId).SingleOrDefaultAsync(cancellationToken);
        return projectId.HasValue && await CanReadAsync(userId, projectId.Value, cancellationToken) ? await GetResponseAsync(milestoneId, cancellationToken) : null;
    }

    public async Task<MilestoneResponse?> UpdateAsync(Guid userId, Guid milestoneId, UpdateMilestoneRequest request, CancellationToken cancellationToken)
    {
        var milestone = await dbContext.ProjectMilestones.SingleOrDefaultAsync(item => item.Id == milestoneId, cancellationToken);
        if (milestone is null || !await CanManageAsync(userId, milestone.ProjectId, cancellationToken) || request.DueAt <= DateTimeOffset.UtcNow ||
            await dbContext.ProjectMilestones.AnyAsync(item => item.ProjectId == milestone.ProjectId && item.Sequence == request.Sequence && item.Id != milestoneId, cancellationToken))
        {
            return null;
        }

        try { milestone.Update(request.Title, request.Description, request.Sequence, request.DueAt, request.Version); }
        catch (InvalidOperationException) { return null; }
        AddEvent(milestone.ProjectId, "MILESTONE_UPDATED", userId, milestone.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetResponseAsync(milestone.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid milestoneId, Guid version, CancellationToken cancellationToken)
    {
        var milestone = await dbContext.ProjectMilestones.SingleOrDefaultAsync(item => item.Id == milestoneId, cancellationToken);
        if (milestone is null || !await CanManageAsync(userId, milestone.ProjectId, cancellationToken) || milestone.Status != MilestoneStatus.PLANNED || milestone.Version != version)
        {
            return false;
        }

        AddEvent(milestone.ProjectId, "MILESTONE_DELETED", userId, milestone.Id);
        dbContext.ProjectMilestones.Remove(milestone);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MilestoneResponse?> TransitionAsync(Guid userId, Guid milestoneId, string action, TransitionMilestoneRequest request, CancellationToken cancellationToken)
    {
        var milestone = await dbContext.ProjectMilestones.SingleOrDefaultAsync(item => item.Id == milestoneId, cancellationToken);
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
                    if (!await dbContext.MilestoneDeliverables.AnyAsync(item => item.MilestoneId == milestone.Id, cancellationToken) ||
                        await dbContext.MilestoneDeliverables.AnyAsync(item => item.MilestoneId == milestone.Id &&
                            (item.FileId == null || !dbContext.FileRecords.Any(file => file.Id == item.FileId && file.ProjectId == milestone.ProjectId && file.Status == FileUploadStatus.COMPLETED)), cancellationToken)) return null;
                    milestone.Submit(request.Version);
                    break;
                case "request-revision": milestone.RequestRevision(request.Version); break;
                case "approve": milestone.Approve(request.Version); break;
                default: return null;
            }
        }
        catch (InvalidOperationException) { return null; }

        var updated = await dbContext.ProjectMilestones.Where(item => item.Id == milestone.Id && item.Version == persistedVersion)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, milestone.Status)
                .SetProperty(item => item.Version, milestone.Version), cancellationToken);
        if (updated != 1) return null;

        dbContext.Entry(milestone).State = EntityState.Detached;
        dbContext.MilestoneApprovalHistories.Add(new MilestoneApprovalHistory(milestone.Id, fromStatus, milestone.Status, userId, request.Note));
        AddEvent(milestone.ProjectId, $"MILESTONE_{action.ToUpperInvariant().Replace('-', '_')}", userId, milestone.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(milestone, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MilestoneDeliverableResponse>?> ListDeliverablesAsync(Guid userId, Guid milestoneId, CancellationToken cancellationToken)
    {
        var milestone = await dbContext.ProjectMilestones.AsNoTracking().SingleOrDefaultAsync(item => item.Id == milestoneId, cancellationToken);
        if (milestone is null || !await CanReadAsync(userId, milestone.ProjectId, cancellationToken)) return null;
        return await dbContext.MilestoneDeliverables.AsNoTracking().Where(item => item.MilestoneId == milestoneId).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new MilestoneDeliverableResponse(item.Id, item.MilestoneId, item.Name, item.Criteria, item.FileId)).ToListAsync(cancellationToken);
    }

    public async Task<MilestoneDeliverableResponse?> AddDeliverableAsync(Guid userId, Guid milestoneId, CreateMilestoneDeliverableRequest request, CancellationToken cancellationToken)
    {
        var milestone = await dbContext.ProjectMilestones.SingleOrDefaultAsync(item => item.Id == milestoneId, cancellationToken);
        if (milestone is null || milestone.Status == MilestoneStatus.APPROVED || !await CanContributeAsync(userId, milestone.ProjectId, cancellationToken) ||
            !await IsAuthorizedCompletedFileAsync(userId, milestone.ProjectId, request.FileId, cancellationToken)) return null;
        try
        {
            var deliverable = new MilestoneDeliverable(milestoneId, request.Name, request.Criteria, request.FileId);
            dbContext.MilestoneDeliverables.Add(deliverable);
            AddEvent(milestone.ProjectId, "MILESTONE_DELIVERABLE_CREATED", userId, milestone.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new MilestoneDeliverableResponse(deliverable.Id, deliverable.MilestoneId, deliverable.Name, deliverable.Criteria, deliverable.FileId);
        }
        catch (ArgumentException) { return null; }
    }

    public async Task<MilestoneDeliverableResponse?> UpdateDeliverableAsync(Guid userId, Guid deliverableId, UpdateMilestoneDeliverableRequest request, CancellationToken cancellationToken)
    {
        var row = await (from deliverable in dbContext.MilestoneDeliverables
                         join milestone in dbContext.ProjectMilestones on deliverable.MilestoneId equals milestone.Id
                         where deliverable.Id == deliverableId
                         select new { deliverable, milestone }).SingleOrDefaultAsync(cancellationToken);
        if (row is null || row.milestone.Status == MilestoneStatus.APPROVED || !await CanContributeAsync(userId, row.milestone.ProjectId, cancellationToken) ||
            !await IsAuthorizedCompletedFileAsync(userId, row.milestone.ProjectId, request.FileId, cancellationToken)) return null;
        try { row.deliverable.Update(request.Name, request.Criteria, request.FileId); }
        catch (ArgumentException) { return null; }
        AddEvent(row.milestone.ProjectId, "MILESTONE_DELIVERABLE_UPDATED", userId, row.milestone.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MilestoneDeliverableResponse(row.deliverable.Id, row.deliverable.MilestoneId, row.deliverable.Name, row.deliverable.Criteria, row.deliverable.FileId);
    }

    public async Task<bool> DeleteDeliverableAsync(Guid userId, Guid deliverableId, CancellationToken cancellationToken)
    {
        var row = await (from deliverable in dbContext.MilestoneDeliverables
                         join milestone in dbContext.ProjectMilestones on deliverable.MilestoneId equals milestone.Id
                         where deliverable.Id == deliverableId
                         select new { deliverable, milestone }).SingleOrDefaultAsync(cancellationToken);
        if (row is null || row.milestone.Status == MilestoneStatus.APPROVED || !await CanContributeAsync(userId, row.milestone.ProjectId, cancellationToken)) return false;
        AddEvent(row.milestone.ProjectId, "MILESTONE_DELIVERABLE_DELETED", userId, row.milestone.Id);
        dbContext.MilestoneDeliverables.Remove(row.deliverable);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<MilestoneResponse?> GetResponseAsync(Guid milestoneId, CancellationToken cancellationToken)
    {
        var milestone = await dbContext.ProjectMilestones.AsNoTracking().SingleOrDefaultAsync(item => item.Id == milestoneId, cancellationToken);
        return milestone is null ? null : await BuildResponseAsync(milestone, cancellationToken);
    }

    private async Task<MilestoneResponse> BuildResponseAsync(ProjectMilestone milestone, CancellationToken cancellationToken)
    {
        var deliverables = await dbContext.MilestoneDeliverables.AsNoTracking().Where(item => item.MilestoneId == milestone.Id).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new MilestoneDeliverableResponse(item.Id, item.MilestoneId, item.Name, item.Criteria, item.FileId)).ToListAsync(cancellationToken);
        var history = await dbContext.MilestoneApprovalHistories.AsNoTracking().Where(item => item.MilestoneId == milestone.Id).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new MilestoneApprovalHistoryResponse(item.Id, item.FromStatus, item.ToStatus, item.ActorUserId, item.Note, item.CreatedAt)).ToListAsync(cancellationToken);
        return new MilestoneResponse(milestone.Id, milestone.ProjectId, milestone.Title, milestone.Description, milestone.Sequence, milestone.DueAt, milestone.Status,
            milestone.Status != MilestoneStatus.APPROVED && milestone.DueAt < DateTimeOffset.UtcNow, milestone.Version, milestone.CreatedAt, deliverables, history);
    }

    private async Task<bool> IsAuthorizedCompletedFileAsync(Guid userId, Guid projectId, Guid? fileId, CancellationToken cancellationToken) =>
        !fileId.HasValue || await dbContext.FileRecords.AnyAsync(file => file.Id == fileId.Value && file.ProjectId == projectId && file.UploadedByUserId == userId && file.Status == FileUploadStatus.COMPLETED, cancellationToken);

    private async Task<bool> CanReadAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken) ||
        await dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member => member.UserId == userId), cancellationToken) ||
        await dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles, assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId, cancellationToken);

    private async Task<bool> CanContributeAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) => await CanReadAsync(userId, projectId, cancellationToken);

    private async Task<bool> CanManageAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member => member.UserId == userId && (member.Role == CompanyMemberRole.OWNER || member.Role == CompanyMemberRole.MANAGER)), cancellationToken) ||
        await dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles, assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId, cancellationToken);

    private Task<bool> CanReviewAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) => CanManageAsync(userId, projectId, cancellationToken);

    private void AddEvent(Guid projectId, string eventType, Guid actorUserId, Guid milestoneId)
    {
        dbContext.ProjectActivities.Add(new ProjectActivity(projectId, eventType, actorUserId, JsonSerializer.Serialize(new { milestoneId })));
        dbContext.OutboxMessages.Add(new OutboxMessage("milestone.transitioned", JsonSerializer.Serialize(new { projectId, milestoneId, eventType, actorUserId })));
    }
}
