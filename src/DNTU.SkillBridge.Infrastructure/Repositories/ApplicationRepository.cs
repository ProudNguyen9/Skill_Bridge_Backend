using System.Data;
using DNTU.SkillBridge.Application.Applications;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Applications;
using DNTU.SkillBridge.Domain.Common;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

public sealed class ApplicationRepository(AppDbContext dbContext) : IApplicationRepository
{
    private static readonly ProjectStatus[] ApplyableStatuses =
    [
        ProjectStatus.APPROVED,
        ProjectStatus.RECRUITING,
        ProjectStatus.IN_PROGRESS
    ];

    public async Task<(ApplicationApplyOutcome Outcome, ApplicationResponse? Application)> ApplyToProjectAsync(Guid userId, Guid projectId, CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (ApplicationApplyOutcome.StudentNotFound, null);

        var project = await dbContext.Projects.AsTracking().SingleOrDefaultAsync(project => project.Id == projectId && project.IsActive, cancellationToken);
        if (project is null || !ApplyableStatuses.Contains(project.Status)) return (ApplicationApplyOutcome.ProjectNotFound, null);
        if (project.ApplicationDeadline.HasValue && project.ApplicationDeadline.Value <= DateTimeOffset.UtcNow) return (ApplicationApplyOutcome.ProjectNotAccepting, null);

        var hasAbandonedCommitment = await dbContext.ProjectCommitments.AsNoTracking()
            .AnyAsync(commitment => commitment.StudentId == studentId.Value && (commitment.Status == CommitmentStatus.ABANDONED || commitment.Status == CommitmentStatus.EXPIRED), cancellationToken);
        if (hasAbandonedCommitment) return (ApplicationApplyOutcome.ProjectNotAccepting, null);

        if (request.TeamId is Guid teamId)
        {
            var team = await dbContext.Teams.AsNoTracking().Include(item => item.Members).SingleOrDefaultAsync(item => item.Id == teamId && !item.IsLocked, cancellationToken);
            if (team is null || !team.Members.Any(member => member.StudentId == studentId.Value && member.Role == DNTU.SkillBridge.Domain.Teams.TeamMemberRole.LEADER)
                || team.Members.Count < project.MinTeamSize || team.Members.Count > project.MaxTeamSize)
            {
                return (ApplicationApplyOutcome.ProjectNotAccepting, null);
            }
        }

        var application = new Domain.Applications.Application(project.Id, studentId.Value, request.CoverLetter, request.TeamId);
        dbContext.Applications.Add(application);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (ApplicationApplyOutcome.AlreadyApplied, null);
        }

        return (ApplicationApplyOutcome.Applied, await ProjectApplicationAsync(application.Id, studentId.Value, cancellationToken));
    }

    public async Task<ApplicationResponse?> GetMyApplicationAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        return studentId is null ? null : await ProjectApplicationAsync(applicationId, studentId.Value, cancellationToken);
    }

    public async Task<PagedResponse<ApplicationResponse>> ListMyApplicationsAsync(Guid userId, ApplicationListQuery query, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return new PagedResponse<ApplicationResponse>([], PageMetadata.Create(query.Page, query.PageSize, 0));

        var baseQuery = dbContext.Applications.AsNoTracking().Where(application => application.StudentId == studentId.Value);
        if (Enum.TryParse<ApplicationStatus>(query.Status?.Trim(), ignoreCase: true, out var status)) baseQuery = baseQuery.Where(application => application.Status == status);

        var totalItems = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery.OrderByDescending(application => application.CreatedAt).ThenByDescending(application => application.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(application => new
            {
                application.Id,
                application.ProjectId,
                application.Status,
                application.CoverLetter,
                application.CreatedAt,
                application.WithdrawnAt,
                ProjectTitle = application.Project.Title,
                ProjectSlug = application.Project.Slug,
                CompanyName = application.Project.Company.Name
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new ApplicationResponse(row.Id, row.ProjectId, row.ProjectTitle, row.ProjectSlug, row.CompanyName, row.Status.ToString(), row.CoverLetter, row.CreatedAt, row.WithdrawnAt)).ToList();
        return new PagedResponse<ApplicationResponse>(items, PageMetadata.Create(query.Page, query.PageSize, totalItems));
    }

    public async Task<(ApplicationWithdrawOutcome Outcome, ApplicationResponse? Application)> WithdrawMyApplicationAsync(Guid userId, Guid applicationId, string? reason, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (ApplicationWithdrawOutcome.NotFound, null);

        var application = await dbContext.Applications.AsTracking().SingleOrDefaultAsync(item => item.Id == applicationId && item.StudentId == studentId.Value, cancellationToken);
        if (application is null) return (ApplicationWithdrawOutcome.NotFound, null);
        if (application.Status != ApplicationStatus.PENDING) return (ApplicationWithdrawOutcome.NotWithdrawable, null);

        application.Withdraw(DateTimeOffset.UtcNow, reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (ApplicationWithdrawOutcome.Withdrawn, await ProjectApplicationAsync(application.Id, studentId.Value, cancellationToken));
    }

    public async Task<PagedResponse<CompanyApplicationResponse>> ListCompanyApplicationsAsync(Guid companyId, CompanyApplicationListQuery query, CancellationToken cancellationToken)
    {
        var baseQuery = dbContext.Applications.AsNoTracking().Where(application => application.Project.CompanyId == companyId && application.Project.IsActive);
        if (query.ProjectId.HasValue) baseQuery = baseQuery.Where(application => application.ProjectId == query.ProjectId.Value);
        if (Enum.TryParse<ApplicationStatus>(query.Status?.Trim(), true, out var status)) baseQuery = baseQuery.Where(application => application.Status == status);

        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery.OrderByDescending(application => application.CreatedAt).ThenByDescending(application => application.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(application => new CompanyApplicationRow(
                application.Id,
                application.ProjectId,
                application.Status,
                application.CreatedAt,
                application.DecidedAt,
                application.DecisionReason,
                application.Project.Title,
                application.Project.Slug,
                dbContext.Users.Where(user => user.Id == application.Student.UserId).Select(user => user.DisplayName).FirstOrDefault() ?? DisplayNameFallbacks.Student,
                application.Student.StudentCode,
                application.Student.CvUrl,
                application.Student.PortfolioUrl,
                application.Student.GithubUrl,
                application.CoverLetter))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CompanyApplicationResponse>(rows.Select(MapCompanyApplication).ToList(), PageMetadata.Create(query.Page, query.PageSize, total));
    }

    public async Task<CompanyApplicationResponse?> GetCompanyApplicationAsync(Guid companyId, Guid applicationId, CancellationToken cancellationToken)
    {
        var row = await dbContext.Applications.AsNoTracking()
            .Where(application => application.Id == applicationId && application.Project.CompanyId == companyId)
            .Select(application => new CompanyApplicationRow(
                application.Id,
                application.ProjectId,
                application.Status,
                application.CreatedAt,
                application.DecidedAt,
                application.DecisionReason,
                application.Project.Title,
                application.Project.Slug,
                dbContext.Users.Where(user => user.Id == application.Student.UserId).Select(user => user.DisplayName).FirstOrDefault() ?? DisplayNameFallbacks.Student,
                application.Student.StudentCode,
                application.Student.CvUrl,
                application.Student.PortfolioUrl,
                application.Student.GithubUrl,
                application.CoverLetter))
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : MapCompanyApplication(row);
    }

    public async Task<(ApplicationWithdrawOutcome Outcome, CompanyApplicationResponse? Application)> DecideCompanyApplicationAsync(Guid companyId, Guid companyUserId, Guid applicationId, ApplicationStatus targetStatus, string? reason, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var application = await dbContext.Applications.AsTracking()
            .Include(item => item.Project)
            .Include(item => item.Team).ThenInclude(team => team!.Members)
            .SingleOrDefaultAsync(item => item.Id == applicationId && item.Project.CompanyId == companyId, cancellationToken);
        if (application is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationWithdrawOutcome.NotFound, null);
        }

        try
        {
            if (targetStatus == ApplicationStatus.ACCEPTED)
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM projects WHERE \"Id\" = {application.ProjectId} FOR UPDATE", cancellationToken);
                var selectedStudentIds = application.Team?.Members.Select(member => member.StudentId).ToArray() ?? [application.StudentId];
                var acceptedStudentCount = await dbContext.Applications.Where(item => item.ProjectId == application.ProjectId && item.Status == ApplicationStatus.ACCEPTED)
                    .SumAsync(item => item.TeamId.HasValue ? item.Team!.Members.Count : 1, cancellationToken);
                if (acceptedStudentCount + selectedStudentIds.Length > application.Project.ExpectedStudentCount)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (ApplicationWithdrawOutcome.NotWithdrawable, null);
                }

                var alreadyAcceptedElsewhere = await dbContext.Applications.AnyAsync(item => selectedStudentIds.Contains(item.StudentId) && item.ProjectId != application.ProjectId && item.Status == ApplicationStatus.ACCEPTED, cancellationToken);
                var acceptedTeamContainsSelectedStudent = await dbContext.Applications.AnyAsync(item => item.ProjectId != application.ProjectId && item.Status == ApplicationStatus.ACCEPTED && item.TeamId.HasValue && dbContext.TeamMembers.Any(member => member.TeamId == item.TeamId && selectedStudentIds.Contains(member.StudentId)), cancellationToken);
                var alreadyActiveElsewhere = await dbContext.ProjectMembers.AnyAsync(member => selectedStudentIds.Contains(member.StudentId) && member.ProjectId != application.ProjectId && member.IsActive, cancellationToken);
                if (alreadyAcceptedElsewhere || acceptedTeamContainsSelectedStudent || alreadyActiveElsewhere)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (ApplicationWithdrawOutcome.NotWithdrawable, null);
                }
            }

            var now = DateTimeOffset.UtcNow;
            switch (targetStatus)
            {
                case ApplicationStatus.SHORTLISTED: application.Shortlist(companyUserId, now, reason); break;
                case ApplicationStatus.REJECTED: application.Reject(companyUserId, now, reason); break;
                case ApplicationStatus.ACCEPTED: application.Accept(companyUserId, now, reason); break;
                default:
                    await transaction.RollbackAsync(cancellationToken);
                    return (ApplicationWithdrawOutcome.NotWithdrawable, null);
            }

            if (targetStatus == ApplicationStatus.ACCEPTED)
            {
                application.Team?.Lock();
                foreach (var studentId in application.Team?.Members.Select(member => member.StudentId).ToArray() ?? [application.StudentId])
                {
                    dbContext.ProjectCommitments.Add(new ProjectCommitment(application.ProjectId, studentId, now.AddDays(7)));
                }
            }

            dbContext.ProjectActivities.Add(new ProjectActivity(application.ProjectId, $"APPLICATION_{targetStatus}", companyUserId));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationWithdrawOutcome.NotWithdrawable, null);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationWithdrawOutcome.NotWithdrawable, null);
        }

        return (ApplicationWithdrawOutcome.Withdrawn, await GetCompanyApplicationAsync(companyId, applicationId, cancellationToken));
    }

    private async Task<Guid?> ResolveStudentIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.StudentProfiles.AsTracking().Include(item => item.Privacy).SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new Domain.Students.StudentProfile(userId);
            dbContext.StudentProfiles.Add(profile);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return profile.Id;
    }

    private async Task<ApplicationResponse?> ProjectApplicationAsync(Guid applicationId, Guid studentId, CancellationToken cancellationToken)
    {
        var row = await dbContext.Applications.AsNoTracking()
            .Where(application => application.Id == applicationId && application.StudentId == studentId)
            .Select(application => new
            {
                application.Id,
                application.ProjectId,
                application.Status,
                application.CoverLetter,
                application.CreatedAt,
                application.WithdrawnAt,
                ProjectTitle = application.Project.Title,
                ProjectSlug = application.Project.Slug,
                CompanyName = application.Project.Company.Name
            })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new ApplicationResponse(row.Id, row.ProjectId, row.ProjectTitle, row.ProjectSlug, row.CompanyName, row.Status.ToString(), row.CoverLetter, row.CreatedAt, row.WithdrawnAt);
    }

    private static CompanyApplicationResponse MapCompanyApplication(CompanyApplicationRow row) => new(
        row.Id,
        row.ProjectId,
        row.ProjectTitle,
        row.ProjectSlug,
        row.Status.ToString(),
        row.ApplicantDisplayName,
        row.StudentCode,
        row.CvUrl,
        row.PortfolioUrl,
        row.GithubUrl,
        row.Proposal,
        row.CreatedAt,
        row.DecidedAt,
        row.DecisionReason);

    private sealed record CompanyApplicationRow(
        Guid Id,
        Guid ProjectId,
        ApplicationStatus Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? DecidedAt,
        string? DecisionReason,
        string ProjectTitle,
        string ProjectSlug,
        string ApplicantDisplayName,
        string? StudentCode,
        string? CvUrl,
        string? PortfolioUrl,
        string? GithubUrl,
        string? Proposal);
}
