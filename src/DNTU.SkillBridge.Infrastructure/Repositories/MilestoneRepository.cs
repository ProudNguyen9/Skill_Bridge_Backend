using DNTU.SkillBridge.Application.Milestones;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Files;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Milestones;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the project milestone data operations.</summary>
public sealed class MilestoneRepository(AppDbContext dbContext) : IMilestoneRepository
{
    public Task<bool> HasMilestoneWithSequenceAsync(Guid projectId, int sequence, CancellationToken cancellationToken) =>
        dbContext.ProjectMilestones.AnyAsync(item => item.ProjectId == projectId && item.Sequence == sequence, cancellationToken);

    public Task<bool> HasOtherMilestoneWithSequenceAsync(Guid projectId, int sequence, Guid milestoneId, CancellationToken cancellationToken) =>
        dbContext.ProjectMilestones.AnyAsync(item => item.ProjectId == projectId && item.Sequence == sequence && item.Id != milestoneId, cancellationToken);

    public void AddMilestone(ProjectMilestone milestone) => dbContext.ProjectMilestones.Add(milestone);

    public void RemoveMilestone(ProjectMilestone milestone) => dbContext.ProjectMilestones.Remove(milestone);

    public Task<Guid?> FindProjectIdByMilestoneIdAsync(Guid milestoneId, CancellationToken cancellationToken) =>
        dbContext.ProjectMilestones.AsNoTracking().Where(item => item.Id == milestoneId).Select(item => (Guid?)item.ProjectId).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> ListMilestoneIdsAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMilestones.AsNoTracking().Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.Sequence).ThenBy(item => item.Id).Select(item => item.Id).ToListAsync(cancellationToken);

    public Task<ProjectMilestone?> FindMilestoneAsync(Guid milestoneId, CancellationToken cancellationToken) =>
        dbContext.ProjectMilestones.SingleOrDefaultAsync(item => item.Id == milestoneId, cancellationToken);

    public Task<ProjectMilestone?> FindMilestoneNoTrackingAsync(Guid milestoneId, CancellationToken cancellationToken) =>
        dbContext.ProjectMilestones.AsNoTracking().SingleOrDefaultAsync(item => item.Id == milestoneId, cancellationToken);

    public async Task<(MilestoneDeliverable Deliverable, ProjectMilestone Milestone)?> FindDeliverableWithMilestoneAsync(Guid deliverableId, CancellationToken cancellationToken)
    {
        var row = await (from deliverable in dbContext.MilestoneDeliverables
                         join milestone in dbContext.ProjectMilestones on deliverable.MilestoneId equals milestone.Id
                         where deliverable.Id == deliverableId
                         select new { deliverable, milestone }).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.deliverable, row.milestone);
    }

    public void AddDeliverable(MilestoneDeliverable deliverable) => dbContext.MilestoneDeliverables.Add(deliverable);

    public void RemoveDeliverable(MilestoneDeliverable deliverable) => dbContext.MilestoneDeliverables.Remove(deliverable);

    public Task<bool> HasDeliverableAsync(Guid milestoneId, CancellationToken cancellationToken) =>
        dbContext.MilestoneDeliverables.AnyAsync(item => item.MilestoneId == milestoneId, cancellationToken);

    public Task<bool> HasDeliverableWithMissingCompletedFileAsync(Guid milestoneId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.MilestoneDeliverables.AnyAsync(item => item.MilestoneId == milestoneId &&
            (item.FileId == null || !dbContext.FileRecords.Any(file => file.Id == item.FileId && file.ProjectId == projectId && file.Status == FileUploadStatus.COMPLETED)), cancellationToken);

    public Task<bool> HasCompletedFileAsync(Guid userId, Guid projectId, Guid fileId, CancellationToken cancellationToken) =>
        dbContext.FileRecords.AnyAsync(file => file.Id == fileId && file.ProjectId == projectId && file.UploadedByUserId == userId && file.Status == FileUploadStatus.COMPLETED, cancellationToken);

    public Task<bool> HasActiveMemberForUserAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken);

    public Task<bool> IsCompanyMemberAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member => member.UserId == userId), cancellationToken);

    public Task<bool> IsCompanyManagerAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member => member.UserId == userId && (member.Role == CompanyMemberRole.OWNER || member.Role == CompanyMemberRole.MANAGER)), cancellationToken);

    public Task<bool> IsActiveLecturerAssignedAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles, assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId, cancellationToken);

    public async Task<IReadOnlyCollection<MilestoneDeliverableResponse>> ListDeliverableResponsesAsync(Guid milestoneId, CancellationToken cancellationToken) =>
        await dbContext.MilestoneDeliverables.AsNoTracking().Where(item => item.MilestoneId == milestoneId).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new MilestoneDeliverableResponse(item.Id, item.MilestoneId, item.Name, item.Criteria, item.FileId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<MilestoneApprovalHistoryResponse>> ListApprovalHistoryResponsesAsync(Guid milestoneId, CancellationToken cancellationToken) =>
        await dbContext.MilestoneApprovalHistories.AsNoTracking().Where(item => item.MilestoneId == milestoneId).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new MilestoneApprovalHistoryResponse(item.Id, item.FromStatus, item.ToStatus, item.ActorUserId, item.Note, item.CreatedAt)).ToListAsync(cancellationToken);

    public void AddApprovalHistory(MilestoneApprovalHistory history) => dbContext.MilestoneApprovalHistories.Add(history);

    public void AddProjectActivity(ProjectActivity activity) => dbContext.ProjectActivities.Add(activity);

    public void AddOutboxMessage(OutboxMessage message) => dbContext.OutboxMessages.Add(message);

    public Task<int> UpdateStatusAndVersionAsync(Guid milestoneId, Guid persistedVersion, MilestoneStatus status, Guid version, CancellationToken cancellationToken) =>
        dbContext.ProjectMilestones.Where(item => item.Id == milestoneId && item.Version == persistedVersion)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, status)
                .SetProperty(item => item.Version, version), cancellationToken);

    public void Detach(ProjectMilestone milestone) => dbContext.Entry(milestone).State = EntityState.Detached;
}
