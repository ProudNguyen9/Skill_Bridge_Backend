using System.Text.Json;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Application.Projects;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Applications;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Payments;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>Coordinates the irreversible project completion transaction and its downstream records.</summary>
public sealed class ProjectCompletionRepository(
    AppDbContext dbContext,
    IProjectActivityWriter activityWriter,
    INotificationWriter? notificationWriter = null) : IProjectCompletionRepository
{
    public async Task<(ProjectCompletionOutcome Outcome, ProjectCompletionResponse? Completion)> CompleteAsync(
        Guid actorUserId,
        bool isAdministrator,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
        {
            return (ProjectCompletionOutcome.NotFound, null);
        }

        if (!isAdministrator && !await IsAssignedLecturerAsync(actorUserId, projectId, cancellationToken))
        {
            return (ProjectCompletionOutcome.Forbidden, null);
        }

        var existing = await dbContext.ProjectCompletions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);
        if (existing is not null)
        {
            return (ProjectCompletionOutcome.Success, Map(existing));
        }

        if (!await PrerequisitesSatisfiedAsync(projectId, cancellationToken))
        {
            return (ProjectCompletionOutcome.Conflict, null);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            project.Complete();
            var boundTeams = await dbContext.Teams
                .Where(t => t.TargetProjectId == projectId ||
                            dbContext.Applications.Any(a => a.ProjectId == projectId &&
                                                            a.Status == ApplicationStatus.ACCEPTED &&
                                                            a.TeamId == t.Id))
                .ToListAsync(cancellationToken);
            foreach (var team in boundTeams)
            {
                team.ReleaseFromCompletedProject();
            }

            var completedStudentIds = await dbContext.ProjectMembers
                .Where(item => item.ProjectId == projectId && item.IsActive)
                .Select(item => item.StudentId)
                .ToListAsync(cancellationToken);
            var evidenceJson = JsonSerializer.Serialize(new
            {
                projectId,
                completedStudentIds,
                completedAt = DateTimeOffset.UtcNow
            });
            var completion = new ProjectCompletion(projectId, actorUserId, evidenceJson);
            dbContext.ProjectCompletions.Add(completion);

            if (project.AllowanceAmount is > 0 && completedStudentIds.Count > 0)
            {
                var amount = project.AllowanceAmount.Value / completedStudentIds.Count;
                if (amount > 0)
                {
                    foreach (var studentId in completedStudentIds)
                    {
                        dbContext.Disbursements.Add(new Disbursement(projectId, studentId, amount, project.AllowanceCurrency ?? "VND"));
                    }
                }
            }

            activityWriter.Append(projectId, "PROJECT_COMPLETED", actorUserId, new { completionId = completion.Id });
            dbContext.OutboxMessages.Add(new OutboxMessage("project.completed", evidenceJson));

            if (notificationWriter is not null)
            {
                var studentUserIds = await dbContext.ProjectMembers.AsNoTracking()
                    .Where(m => m.ProjectId == projectId && m.IsActive)
                    .Select(m => m.Student.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                var studentPayload = JsonSerializer.Serialize(new
                {
                    projectId,
                    projectTitle = project.Title,
                    url = "/student/portfolio",
                });
                foreach (var studentUserId in studentUserIds)
                {
                    notificationWriter.Add(new NotificationMessage(
                        studentUserId,
                        "PROJECT_COMPLETED",
                        $"Dự án “{project.Title}” đã hoàn thành nghiệm thu!",
                        studentPayload));
                }

                var companyUserIds = await dbContext.CompanyMembers.AsNoTracking()
                    .Where(m => m.CompanyId == project.CompanyId)
                    .Select(m => m.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                var companyPayload = JsonSerializer.Serialize(new
                {
                    projectId,
                    projectTitle = project.Title,
                    url = $"/company/projects/{projectId}",
                });
                foreach (var companyUserId in companyUserIds)
                {
                    notificationWriter.Add(new NotificationMessage(
                        companyUserId,
                        "PROJECT_COMPLETED",
                        $"Dự án “{project.Title}” đã hoàn tất và đóng sổ nghiệm thu",
                        companyPayload));
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (ProjectCompletionOutcome.Success, Map(completion));
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ProjectCompletionOutcome.Conflict, null);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ProjectCompletionOutcome.Conflict, null);
        }
    }

    public async Task<ProjectCompletionResponse?> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var completion = await dbContext.ProjectCompletions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);
        return completion is null ? null : Map(completion);
    }

    private async Task<bool> PrerequisitesSatisfiedAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var hasUnapprovedMilestone = await dbContext.ProjectMilestones
            .AnyAsync(item => item.ProjectId == projectId && item.Status != Domain.Milestones.MilestoneStatus.APPROVED, cancellationToken);
        if (hasUnapprovedMilestone)
        {
            return false;
        }

        if (!await dbContext.ProjectSubmissions.AnyAsync(item => item.ProjectId == projectId && item.Status == SubmissionStatus.BUSINESS_ACCEPTED, cancellationToken))
        {
            return false;
        }

        var activeStudentIds = await dbContext.ProjectMembers.Where(item => item.ProjectId == projectId && item.IsActive)
            .Select(item => item.StudentId).ToListAsync(cancellationToken);
        return activeStudentIds.Count > 0 && await dbContext.AcademicEvaluations
            .CountAsync(item => item.ProjectId == projectId && item.Status == Domain.Academics.AcademicEvaluationStatus.FINALIZED && activeStudentIds.Contains(item.StudentId), cancellationToken) == activeStudentIds.Count;
    }

    private Task<bool> IsAssignedLecturerAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles, assignment => assignment.LecturerId, profile => profile.Id, (assignment, profile) => new { assignment, profile })
            .AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.profile.IsActive && row.profile.UserId == userId, cancellationToken);

    private static ProjectCompletionResponse Map(ProjectCompletion completion) => new(completion.Id, completion.ProjectId, completion.CompletedByUserId, completion.CreatedAt, completion.EvidenceJson);
}
