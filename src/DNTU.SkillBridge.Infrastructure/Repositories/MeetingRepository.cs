using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using DNTU.SkillBridge.Application.Meetings;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Meetings;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using DNTU.SkillBridge.Application.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

public sealed class MeetingRepository(AppDbContext dbContext) : IMeetingRepository
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ConversionLocks = new();
    public async Task<MeetingResponse?> CreateAsync(Guid userId, Guid projectId, CreateMeetingRequest request, CancellationToken cancellationToken)
    {
        if (!await CanScheduleAsync(userId, projectId, cancellationToken) || !IsFutureUtcRange(request.StartAt, request.EndAt)) return null;
        var participantIds = request.ParticipantStudentIds.Distinct().ToArray();
        if (participantIds.Length == 0 || !await AreActiveMembersAsync(projectId, participantIds, cancellationToken)) return null;

        try
        {
            var meeting = new ProjectMeeting(projectId, request.Title, request.Description, request.StartAt, request.EndAt, request.ExternalUrl);
            dbContext.ProjectMeetings.Add(meeting);
            foreach (var studentId in participantIds) dbContext.MeetingParticipants.Add(new MeetingParticipant(meeting.Id, studentId));
            AddEvent(meeting, participantIds, "MEETING_CREATED", userId);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Map(meeting);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyCollection<MeetingResponse>?> ListAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!await CanReadAsync(userId, projectId, cancellationToken)) return null;
        return await dbContext.ProjectMeetings.AsNoTracking().Where(meeting => meeting.ProjectId == projectId).OrderBy(meeting => meeting.StartAt).ThenBy(meeting => meeting.Id)
            .Select(meeting => new MeetingResponse(meeting.Id, meeting.ProjectId, meeting.Title, meeting.Description, meeting.StartAt, meeting.EndAt, meeting.ExternalUrl, meeting.ExternalLinkType, meeting.ReminderState, meeting.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<MeetingResponse?> GetAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        var meeting = await dbContext.ProjectMeetings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        return meeting is not null && await CanReadAsync(userId, meeting.ProjectId, cancellationToken) ? Map(meeting) : null;
    }

    public async Task<MeetingResponse?> UpdateAsync(Guid userId, Guid meetingId, UpdateMeetingRequest request, CancellationToken cancellationToken)
    {
        var meeting = await dbContext.ProjectMeetings.SingleOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null || !await CanManageAsync(userId, meeting.ProjectId, cancellationToken) || !IsFutureUtcRange(request.StartAt, request.EndAt)) return null;

        try
        {
            meeting.Update(request.Title, request.Description, request.StartAt, request.EndAt, request.ExternalUrl);
            var participantIds = await dbContext.MeetingParticipants.Where(item => item.MeetingId == meeting.Id).Select(item => item.StudentId).ToArrayAsync(cancellationToken);
            AddEvent(meeting, participantIds, "MEETING_UPDATED", userId);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Map(meeting);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        var meeting = await dbContext.ProjectMeetings.SingleOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null || !await CanManageAsync(userId, meeting.ProjectId, cancellationToken)) return false;

        meeting.CancelReminder();
        var participantIds = await dbContext.MeetingParticipants.Where(item => item.MeetingId == meeting.Id).Select(item => item.StudentId).ToArrayAsync(cancellationToken);
        AddEvent(meeting, participantIds, "MEETING_CANCELLED", userId);
        dbContext.ProjectMeetings.Remove(meeting);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<MeetingParticipantResponse>?> GetParticipantsAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        var meeting = await dbContext.ProjectMeetings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null || !await CanReadAsync(userId, meeting.ProjectId, cancellationToken)) return null;
        return await dbContext.MeetingParticipants.AsNoTracking().Where(item => item.MeetingId == meetingId).OrderBy(item => item.StudentId)
            .Select(item => new MeetingParticipantResponse(item.StudentId, item.AttendanceStatus, item.UpdatedAt)).ToListAsync(cancellationToken);
    }

    public async Task<bool> UpdateAttendanceAsync(Guid userId, Guid meetingId, UpdateAttendanceRequest request, CancellationToken cancellationToken)
    {
        var meeting = await dbContext.ProjectMeetings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null || !await CanManageAsync(userId, meeting.ProjectId, cancellationToken) || !Enum.IsDefined(request.AttendanceStatus)) return false;
        var participant = await dbContext.MeetingParticipants.SingleOrDefaultAsync(item => item.MeetingId == meetingId && item.StudentId == request.StudentId, cancellationToken);
        if (participant is null) return false;

        participant.SetAttendance(request.AttendanceStatus);
        dbContext.ProjectActivities.Add(new ProjectActivity(meeting.ProjectId, "MEETING_ATTENDANCE_UPDATED", userId,
            JsonSerializer.Serialize(new { meetingId, studentId = request.StudentId, attendanceStatus = request.AttendanceStatus })));
        dbContext.OutboxMessages.Add(new OutboxMessage("meeting.attendance.updated", JsonSerializer.Serialize(new { meetingId, meeting.ProjectId, studentId = request.StudentId, attendanceStatus = request.AttendanceStatus, actorUserId = userId })));
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(MeetingOutcome Outcome, MeetingMinutesResponse? Minutes)> GetMinutesAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        var meeting = await dbContext.ProjectMeetings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return (MeetingOutcome.NotFound, null);
        if (!await CanReadAsync(userId, meeting.ProjectId, cancellationToken)) return (MeetingOutcome.Forbidden, null);
        var minute = await dbContext.MeetingMinutes.AsNoTracking().SingleOrDefaultAsync(item => item.MeetingId == meetingId, cancellationToken);
        return minute is null ? (MeetingOutcome.NotFound, null) : (MeetingOutcome.Success, Map(minute));
    }

    public async Task<(MeetingOutcome Outcome, MeetingMinutesResponse? Minutes)> UpsertMinutesAsync(Guid userId, Guid meetingId, UpsertMeetingMinutesRequest request, CancellationToken cancellationToken)
    {
        var meeting = await dbContext.ProjectMeetings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return (MeetingOutcome.NotFound, null);
        if (!await CanManageAsync(userId, meeting.ProjectId, cancellationToken)) return (MeetingOutcome.Forbidden, null);

        try
        {
            var minute = await dbContext.MeetingMinutes.SingleOrDefaultAsync(item => item.MeetingId == meetingId, cancellationToken);
            if (minute is null)
            {
                minute = new MeetingMinute(meetingId, request.Content, userId);
                dbContext.MeetingMinutes.Add(minute);
            }
            else
            {
                dbContext.MeetingMinuteRevisions.Add(new MeetingMinuteRevision(minute.Id, minute.Content, minute.UpdatedByUserId));
                minute.Update(request.Content, userId);
            }

            AddActivityAndOutbox(meeting.ProjectId, "MEETING_MINUTES_UPDATED", userId, new { meetingId, minuteId = minute.Id });
            await dbContext.SaveChangesAsync(cancellationToken);
            return (MeetingOutcome.Success, Map(minute));
        }
        catch (ArgumentException)
        {
            return (MeetingOutcome.Invalid, null);
        }
    }

    public async Task<(MeetingOutcome Outcome, MeetingActionItemResponse? ActionItem)> CreateActionItemAsync(Guid userId, Guid meetingId, CreateMeetingActionItemRequest request, CancellationToken cancellationToken)
    {
        var meeting = await dbContext.ProjectMeetings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == meetingId, cancellationToken);
        if (meeting is null) return (MeetingOutcome.NotFound, null);
        if (!await CanManageAsync(userId, meeting.ProjectId, cancellationToken)) return (MeetingOutcome.Forbidden, null);
        if (request.ResponsibleStudentId.HasValue && !await IsActiveMemberAsync(meeting.ProjectId, request.ResponsibleStudentId.Value, cancellationToken)) return (MeetingOutcome.InvalidAssignee, null);
        if (!IsActionItemDueDateValid(meeting, request.DueAt)) return (MeetingOutcome.Invalid, null);

        try
        {
            var actionItem = new MeetingActionItem(meetingId, request.Description, request.ResponsibleStudentId, request.DueAt);
            dbContext.MeetingActionItems.Add(actionItem);
            AddActivityAndOutbox(meeting.ProjectId, "MEETING_ACTION_ITEM_CREATED", userId, new { meetingId, actionItemId = actionItem.Id });
            await dbContext.SaveChangesAsync(cancellationToken);
            return (MeetingOutcome.Success, Map(actionItem));
        }
        catch (ArgumentException)
        {
            return (MeetingOutcome.Invalid, null);
        }
    }

    public async Task<(MeetingOutcome Outcome, MeetingActionItemResponse? ActionItem)> UpdateActionItemAsync(Guid userId, Guid actionItemId, UpdateMeetingActionItemRequest request, CancellationToken cancellationToken)
    {
        var actionItem = await dbContext.MeetingActionItems.SingleOrDefaultAsync(item => item.Id == actionItemId, cancellationToken);
        if (actionItem is null) return (MeetingOutcome.NotFound, null);
        var projectId = await ProjectIdForMeetingAsync(actionItem.MeetingId, cancellationToken);
        if (projectId is null) return (MeetingOutcome.NotFound, null);
        if (!await CanManageAsync(userId, projectId.Value, cancellationToken)) return (MeetingOutcome.Forbidden, null);
        if (request.ResponsibleStudentId.HasValue && !await IsActiveMemberAsync(projectId.Value, request.ResponsibleStudentId.Value, cancellationToken)) return (MeetingOutcome.InvalidAssignee, null);
        var meeting = await dbContext.ProjectMeetings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == actionItem.MeetingId, cancellationToken);
        if (meeting is null) return (MeetingOutcome.NotFound, null);
        if (!IsActionItemDueDateValid(meeting, request.DueAt)) return (MeetingOutcome.Invalid, null);

        try
        {
            actionItem.Update(request.Description, request.ResponsibleStudentId, request.DueAt, request.Version);
            actionItem.SetCompleted(request.IsCompleted, actionItem.Version);
            AddActivityAndOutbox(projectId.Value, "MEETING_ACTION_ITEM_UPDATED", userId, new { meetingId = actionItem.MeetingId, actionItemId });
            await dbContext.SaveChangesAsync(cancellationToken);
            return (MeetingOutcome.Success, Map(actionItem));
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            return (MeetingOutcome.Conflict, null);
        }
        catch (ArgumentException)
        {
            return (MeetingOutcome.Invalid, null);
        }
    }

    public async Task<MeetingOutcome> DeleteActionItemAsync(Guid userId, Guid actionItemId, Guid version, CancellationToken cancellationToken)
    {
        var actionItem = await dbContext.MeetingActionItems.SingleOrDefaultAsync(item => item.Id == actionItemId, cancellationToken);
        if (actionItem is null) return MeetingOutcome.NotFound;
        var projectId = await ProjectIdForMeetingAsync(actionItem.MeetingId, cancellationToken);
        if (projectId is null) return MeetingOutcome.NotFound;
        if (!await CanManageAsync(userId, projectId.Value, cancellationToken)) return MeetingOutcome.Forbidden;
        if (actionItem.Version != version || actionItem.ProjectTaskId.HasValue) return MeetingOutcome.Conflict;

        dbContext.MeetingActionItems.Remove(actionItem);
        AddActivityAndOutbox(projectId.Value, "MEETING_ACTION_ITEM_DELETED", userId, new { meetingId = actionItem.MeetingId, actionItemId });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return MeetingOutcome.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return MeetingOutcome.Conflict;
        }
    }

    public async Task<(MeetingOutcome Outcome, MeetingActionItemConversionResponse? Conversion)> ConvertActionItemToTaskAsync(Guid userId, Guid actionItemId, ConvertMeetingActionItemRequest request, CancellationToken cancellationToken)
    {
        // The local keyed lock prevents duplicate writes in a scaled-in process; the conditional
        // database update below remains the cross-instance correctness boundary.
        var gate = ConversionLocks.GetOrAdd(actionItemId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            // The linked task is a one-to-one projection. Serializable isolation prevents two
            // concurrent requests from both observing an unlinked action item.
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var actionItem = await dbContext.MeetingActionItems.SingleOrDefaultAsync(item => item.Id == actionItemId, cancellationToken);
                if (actionItem is null) return (MeetingOutcome.NotFound, null);
                var meeting = await dbContext.ProjectMeetings.SingleOrDefaultAsync(item => item.Id == actionItem.MeetingId, cancellationToken);
                if (meeting is null) return (MeetingOutcome.NotFound, null);
                if (!await CanManageAsync(userId, meeting.ProjectId, cancellationToken)) return (MeetingOutcome.Forbidden, null);

                if (actionItem.ProjectTaskId.HasValue)
                {
                    var linkedTask = await dbContext.ProjectTasks.AsNoTracking().SingleOrDefaultAsync(task => task.Id == actionItem.ProjectTaskId.Value, cancellationToken);
                    return linkedTask is null ? (MeetingOutcome.Conflict, null) : (MeetingOutcome.Success, new MeetingActionItemConversionResponse(Map(actionItem), Map(linkedTask), true));
                }

                if (actionItem.ResponsibleStudentId.HasValue && !await IsActiveMemberAsync(meeting.ProjectId, actionItem.ResponsibleStudentId.Value, cancellationToken)) return (MeetingOutcome.InvalidAssignee, null);
                var order = (await dbContext.ProjectTasks.Where(task => task.ProjectId == meeting.ProjectId && task.Status == ProjectTaskStatus.BACKLOG && !task.IsDeleted).Select(task => (int?)task.SortOrder).MaxAsync(cancellationToken) ?? -1) + 1;
                var task = new ProjectTask(meeting.ProjectId, request.Title ?? actionItem.Description, request.Description, request.Priority, actionItem.DueAt, order);
                if (actionItem.ResponsibleStudentId.HasValue) task.Assign(actionItem.ResponsibleStudentId, task.Version);
                dbContext.ProjectTasks.Add(task);
                await dbContext.SaveChangesAsync(cancellationToken);

                // Claim the conversion with a conditional write. This is the durable concurrency
                // boundary even when multiple application instances process the same request.
                var claimed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE meeting_action_items
                SET "ProjectTaskId" = {task.Id}, "Version" = {Guid.CreateVersion7()}
                WHERE "Id" = {actionItemId} AND "ProjectTaskId" IS NULL
                """, cancellationToken);
                if (claimed != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return await ExistingConversionAsync(actionItemId, cancellationToken);
                }

                AddActivityAndOutbox(meeting.ProjectId, "MEETING_ACTION_ITEM_CONVERTED", userId, new { meetingId = meeting.Id, actionItemId, taskId = task.Id });
                dbContext.ProjectActivities.Add(new ProjectActivity(meeting.ProjectId, "TASK_CREATED_FROM_MEETING_ACTION_ITEM", userId, JsonSerializer.Serialize(new { meetingId = meeting.Id, actionItemId, taskId = task.Id })));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                var conversion = await ExistingConversionAsync(actionItemId, cancellationToken);
                return conversion switch
                {
                    { Outcome: MeetingOutcome.Success, Conversion: not null } => (MeetingOutcome.Success, conversion.Conversion with { AlreadyConverted = false }),
                    _ => conversion
                };
            }
            catch (ArgumentException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (MeetingOutcome.Invalid, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return await ExistingConversionAsync(actionItemId, cancellationToken);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return await ExistingConversionAsync(actionItemId, cancellationToken);
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    // The database can already have aborted a serializable transaction.
                }
                return await ExistingConversionAsync(actionItemId, cancellationToken);
            }
        }
        finally
        {
            gate.Release();
            if (gate.CurrentCount == 1) ConversionLocks.TryRemove(new KeyValuePair<Guid, SemaphoreSlim>(actionItemId, gate));
        }
    }

    private async Task<(MeetingOutcome Outcome, MeetingActionItemConversionResponse? Conversion)> ExistingConversionAsync(Guid actionItemId, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var actionItem = await dbContext.MeetingActionItems.AsNoTracking().SingleOrDefaultAsync(item => item.Id == actionItemId, cancellationToken);
        if (actionItem?.ProjectTaskId is not Guid taskId) return (MeetingOutcome.Conflict, null);
        var task = await dbContext.ProjectTasks.AsNoTracking().SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        return task is null ? (MeetingOutcome.Conflict, null) : (MeetingOutcome.Success, new MeetingActionItemConversionResponse(Map(actionItem), Map(task), true));
    }

    public async Task<IReadOnlyCollection<MeetingResponse>> ListStudentMeetingsAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMeetings.AsNoTracking().Where(meeting => dbContext.ProjectMembers.Any(member => member.ProjectId == meeting.ProjectId && member.IsActive && member.Student.UserId == userId))
            .OrderBy(meeting => meeting.StartAt).ThenBy(meeting => meeting.Id).Select(meeting => new MeetingResponse(meeting.Id, meeting.ProjectId, meeting.Title, meeting.Description, meeting.StartAt, meeting.EndAt, meeting.ExternalUrl, meeting.ExternalLinkType, meeting.ReminderState, meeting.CreatedAt)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<MeetingResponse>> ListCompanyMeetingsAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMeetings.AsNoTracking().Where(meeting => dbContext.Projects.Any(project => project.Id == meeting.ProjectId && project.Company.Members.Any(member => member.UserId == userId && member.Role == CompanyMemberRole.OWNER)))
            .OrderBy(meeting => meeting.StartAt).ThenBy(meeting => meeting.Id).Select(meeting => new MeetingResponse(meeting.Id, meeting.ProjectId, meeting.Title, meeting.Description, meeting.StartAt, meeting.EndAt, meeting.ExternalUrl, meeting.ExternalLinkType, meeting.ReminderState, meeting.CreatedAt)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<MeetingResponse>> ListLecturerMeetingsAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMeetings.AsNoTracking().Where(meeting => dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles, assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer })
            .Any(row => row.assignment.ProjectId == meeting.ProjectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId))
            .OrderBy(meeting => meeting.StartAt).ThenBy(meeting => meeting.Id).Select(meeting => new MeetingResponse(meeting.Id, meeting.ProjectId, meeting.Title, meeting.Description, meeting.StartAt, meeting.EndAt, meeting.ExternalUrl, meeting.ExternalLinkType, meeting.ReminderState, meeting.CreatedAt)).ToListAsync(cancellationToken);

    private static bool IsFutureUtcRange(DateTimeOffset startAt, DateTimeOffset endAt) => startAt.Offset == TimeSpan.Zero && endAt.Offset == TimeSpan.Zero && startAt > DateTimeOffset.UtcNow && endAt > startAt;
    private static bool IsActionItemDueDateValid(ProjectMeeting meeting, DateTimeOffset? dueAt) =>
        dueAt is null || dueAt.Value.Offset == TimeSpan.Zero && dueAt.Value >= meeting.EndAt;

    private async Task<bool> AreActiveMembersAsync(Guid projectId, Guid[] studentIds, CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers.CountAsync(member => member.ProjectId == projectId && member.IsActive && studentIds.Contains(member.StudentId), cancellationToken) == studentIds.Length;

    private Task<bool> IsActiveMemberAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.StudentId == studentId && member.IsActive, cancellationToken);

    private Task<Guid?> ProjectIdForMeetingAsync(Guid meetingId, CancellationToken cancellationToken) =>
        dbContext.ProjectMeetings.Where(meeting => meeting.Id == meetingId).Select(meeting => (Guid?)meeting.ProjectId).SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> CanReadAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await IsAdminAsync(userId, cancellationToken) ||
        await dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken) ||
        await IsCompanyOwnerAsync(userId, projectId, cancellationToken) || await IsAssignedLecturerAsync(userId, projectId, cancellationToken);

    private async Task<bool> CanScheduleAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        if (await CanManageAsync(userId, projectId, cancellationToken)) return true;
        return await dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken) &&
            await dbContext.WorkspaceSettings.AnyAsync(settings => settings.ProjectId == projectId && settings.MembersCanScheduleMeetings, cancellationToken);
    }

    private async Task<bool> CanManageAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await IsAdminAsync(userId, cancellationToken) || await IsCompanyOwnerAsync(userId, projectId, cancellationToken) || await IsAssignedLecturerAsync(userId, projectId, cancellationToken);

    private Task<bool> IsCompanyOwnerAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member => member.UserId == userId && member.Role == CompanyMemberRole.OWNER), cancellationToken);

    private Task<bool> IsAssignedLecturerAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles, assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.lecturer.IsActive && row.lecturer.UserId == userId, cancellationToken);

    private Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.Id == userId && user.UserRoles.Any(userRole => userRole.Role.NormalizedName == RoleNames.Admin || userRole.Role.NormalizedName == RoleNames.SuperAdmin), cancellationToken);

    private void AddEvent(ProjectMeeting meeting, IReadOnlyCollection<Guid> participantIds, string eventType, Guid actorUserId)
    {
        dbContext.ProjectActivities.Add(new ProjectActivity(meeting.ProjectId, eventType, actorUserId, JsonSerializer.Serialize(new { meetingId = meeting.Id })));
        dbContext.OutboxMessages.Add(new OutboxMessage("meeting.reminder.schedule", JsonSerializer.Serialize(new MeetingReminderJob(meeting.Id, meeting.ProjectId, meeting.StartAt, participantIds))));
    }

    private void AddActivityAndOutbox(Guid projectId, string eventType, Guid actorUserId, object payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        dbContext.ProjectActivities.Add(new ProjectActivity(projectId, eventType, actorUserId, payloadJson));
        dbContext.OutboxMessages.Add(new OutboxMessage("meeting.action-item.changed", JsonSerializer.Serialize(new { projectId, eventType, actorUserId, payload })));
    }

    private static MeetingResponse Map(ProjectMeeting meeting) => new(meeting.Id, meeting.ProjectId, meeting.Title, meeting.Description, meeting.StartAt, meeting.EndAt, meeting.ExternalUrl, meeting.ExternalLinkType, meeting.ReminderState, meeting.CreatedAt);
    private static MeetingMinutesResponse Map(MeetingMinute minute) => new(minute.MeetingId, minute.Content, minute.UpdatedByUserId, minute.CreatedAt, minute.UpdatedAt);
    private static MeetingActionItemResponse Map(MeetingActionItem item) => new(item.Id, item.MeetingId, item.Description, item.ResponsibleStudentId, item.DueAt, item.IsCompleted, item.ProjectTaskId, item.Version, item.CreatedAt, item.UpdatedAt);
    private static ProjectTaskResponse Map(ProjectTask task) => new(task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority, task.AssigneeStudentId, task.DueAt, task.SortOrder, task.Version, task.CreatedAt);
}
