using System.Security.Cryptography;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Catalog;
using DNTU.SkillBridge.Domain.Common;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Projects;

public enum ProjectCreateOutcome
{
    Created,
    CompanyNotFound,
    InvalidCatalog,
    InvalidTeamSize,
    Conflict
}

public enum ProjectUpdateOutcome
{
    Updated,
    NotFound,
    NotDraft,
    InvalidCatalog,
    InvalidTeamSize,
    Conflict
}

public enum ProjectDeleteOutcome
{
    Deleted,
    NotFound,
    NotDraft
}

public enum ProjectSubmitOutcome
{
    Submitted,
    NotFound,
    CompanyNotVerified,
    Incomplete,
    InvalidDeadline,
    NotSubmittable
}

public enum ProjectCancelOutcome
{
    Cancelled,
    NotFound,
    InvalidTransition
}

public enum ProjectReopenOutcome
{
    Reopened,
    NotFound,
    InvalidTransition
}

public enum AdminDecisionOutcome
{
    Applied,
    NotFound,
    InvalidTransition
}

/// <summary>
/// Company-scoped project draft CRUD. The company/project identity is always resolved
/// server-side from the authenticated user; request bodies never carry a companyId.
/// Submit/approve/publication transitions belong to Task 13 — this service only ever
/// touches DRAFT projects.
/// </summary>
public sealed class ProjectService(AppDbContext dbContext)
{
    private static readonly string[] AllowedSorts = ["newest", "oldest", "deadline", "title"];

    public async Task<PagedResponse<CompanyProjectListItemResponse>> ListCompanyProjectsAsync(
        Guid companyId, PageQuery query, string? sort, CancellationToken cancellationToken)
    {
        var effectiveSort = AllowedSorts.Contains(sort?.Trim().ToLowerInvariant(), StringComparer.Ordinal)
            ? sort!.Trim().ToLowerInvariant()
            : "newest";

        var totalItems = await dbContext.Projects
            .AsNoTracking()
            .CountAsync(project => project.CompanyId == companyId && project.IsActive, cancellationToken);

        var baseQuery = dbContext.Projects
            .AsNoTracking()
            .Where(project => project.CompanyId == companyId && project.IsActive);

        var orderedQuery = effectiveSort switch
        {
            "oldest" => baseQuery.OrderBy(project => project.CreatedAt).ThenBy(project => project.Id),
            // Nulls last: non-null deadlines sort before null ones, then ascending deadline.
            "deadline" => baseQuery
                .OrderBy(project => project.ApplicationDeadline == null)
                .ThenBy(project => project.ApplicationDeadline)
                .ThenBy(project => project.Id),
            "title" => baseQuery.OrderBy(project => project.NormalizedTitle).ThenBy(project => project.Id),
            _ => baseQuery.OrderByDescending(project => project.CreatedAt).ThenByDescending(project => project.Id)
        };

        var rows = await orderedQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(project => new
            {
                project.Id,
                project.Code,
                project.Title,
                project.Slug,
                project.Status,
                project.Difficulty,
                project.WorkType,
                project.DurationWeeks,
                project.ApplicationDeadline,
                project.ExpectedStudentCount,
                project.AllowanceAmount,
                project.AllowanceCurrency,
                project.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(project => new CompanyProjectListItemResponse(
                project.Id,
                project.Code,
                project.Title,
                project.Slug,
                project.Status.ToString(),
                project.Difficulty,
                project.WorkType,
                project.DurationWeeks,
                project.ApplicationDeadline,
                project.ExpectedStudentCount,
                project.AllowanceAmount,
                project.AllowanceCurrency,
                project.CreatedAt))
            .ToList();

        return new PagedResponse<CompanyProjectListItemResponse>(
            items, PageMetadata.Create(query.Page, query.PageSize, totalItems));
    }

    public async Task<(ProjectCreateOutcome Outcome, CompanyProjectDetailResponse? Project)> CreateProjectAsync(
        Guid companyId, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var companyIsActive = await dbContext.Companies
            .AsNoTracking()
            .AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken);
        if (!companyIsActive)
        {
            return (ProjectCreateOutcome.CompanyNotFound, null);
        }

        if (!IsTeamCapacityValid(request.MinTeamSize, request.MaxTeamSize, request.ExpectedStudentCount))
        {
            return (ProjectCreateOutcome.InvalidTeamSize, null);
        }

        var catalogValidation = await ValidateCatalogAsync(request.IndustryId, request.Skills, cancellationToken);
        if (catalogValidation is not null)
        {
            return (ProjectCreateOutcome.InvalidCatalog, null);
        }

        var project = new Project(
            companyId,
            GenerateProjectCode(),
            request.Title,
            request.Difficulty,
            request.WorkType,
            request.DurationWeeks,
            request.ExpectedStudentCount,
            request.MinTeamSize,
            request.MaxTeamSize,
            request.Summary,
            request.ProblemStatement,
            request.BusinessRequirements,
            request.TechnicalConstraints,
            request.IndustryId,
            request.ApplicationDeadline,
            request.AllowanceAmount,
            request.AllowanceCurrency);

        await AssignUniqueSlugAsync(project.Slug, cancellationToken, project);
        await AssignUniqueCodeAsync(project.Code, cancellationToken, project);
        ApplySkills(project, request.Skills);
        ApplyDeliverables(project, request.Deliverables);

        dbContext.Projects.Add(project);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique slug/code indexes are the final authority under concurrency.
            return (ProjectCreateOutcome.Conflict, null);
        }

        return (ProjectCreateOutcome.Created, await ProjectDetailAsync(companyId, project.Id, cancellationToken));
    }

    /// <summary>Answers null when the project does not exist or belongs to another company (tenant isolation).</summary>
    public async Task<CompanyProjectDetailResponse?> GetCompanyProjectAsync(
        Guid companyId, Guid projectId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(project => project.Id == projectId && project.CompanyId == companyId && project.IsActive, cancellationToken);
        return exists ? await ProjectDetailAsync(companyId, projectId, cancellationToken) : null;
    }

    public async Task<(ProjectUpdateOutcome Outcome, CompanyProjectDetailResponse? Project)> UpdateProjectAsync(
        Guid companyId, Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsTracking()
            .Include(item => item.Skills)
            .Include(item => item.Deliverables)
            .SingleOrDefaultAsync(item => item.Id == projectId && item.CompanyId == companyId && item.IsActive, cancellationToken);
        if (project is null)
        {
            return (ProjectUpdateOutcome.NotFound, null);
        }

        if (project.Status != ProjectStatus.DRAFT)
        {
            return (ProjectUpdateOutcome.NotDraft, null);
        }

        if (!IsTeamCapacityValid(request.MinTeamSize, request.MaxTeamSize, request.ExpectedStudentCount))
        {
            return (ProjectUpdateOutcome.InvalidTeamSize, null);
        }

        var catalogValidation = await ValidateCatalogAsync(request.IndustryId, request.Skills, cancellationToken);
        if (catalogValidation is not null)
        {
            return (ProjectUpdateOutcome.InvalidCatalog, null);
        }

        var oldSlug = project.Slug;

        project.UpdateDraft(
            request.Title,
            request.Summary,
            request.ProblemStatement,
            request.BusinessRequirements,
            request.TechnicalConstraints,
            request.IndustryId,
            request.Difficulty,
            request.WorkType,
            request.DurationWeeks,
            request.ApplicationDeadline,
            request.ExpectedStudentCount,
            request.MinTeamSize,
            request.MaxTeamSize,
            request.AllowanceAmount,
            request.AllowanceCurrency);

        if (!string.Equals(project.Slug, oldSlug, StringComparison.Ordinal))
        {
            await AssignUniqueSlugAsync(project.Slug, cancellationToken, project);
        }

        // Explicit diff through the DbSets: entities discovered via navigation changes with a preset
        // key are not attached as Added, so all adds/removes must go through the DbSet directly.
        var requestedSkills = request.Skills
            .GroupBy(skill => skill.SkillId)
            .Select(group => group.First())
            .ToList();
        foreach (var existing in project.Skills.Where(existing => requestedSkills.All(item => item.SkillId != existing.SkillId)).ToList())
        {
            project.Skills.Remove(existing);
            dbContext.ProjectSkills.Remove(existing);
        }

        foreach (var item in requestedSkills)
        {
            var existing = project.Skills.FirstOrDefault(skill => skill.SkillId == item.SkillId);
            if (existing is null)
            {
                var added = new ProjectSkill(project.Id, item.SkillId, item.RequirementLevel, item.IsRequired);
                project.Skills.Add(added);
                dbContext.ProjectSkills.Add(added);
            }
            else
            {
                existing.SetRequirement(item.RequirementLevel, item.IsRequired);
            }
        }

        foreach (var existing in project.Deliverables.ToList())
        {
            project.Deliverables.Remove(existing);
            dbContext.ProjectDeliverables.Remove(existing);
        }

        foreach (var (deliverable, index) in request.Deliverables.Select((item, index) => (item, index)))
        {
            var added = new ProjectDeliverable(project.Id, deliverable.Name, deliverable.Description, index + 1);
            project.Deliverables.Add(added);
            dbContext.ProjectDeliverables.Add(added);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (ProjectUpdateOutcome.Conflict, null);
        }

        return (ProjectUpdateOutcome.Updated, await ProjectDetailAsync(companyId, projectId, cancellationToken));
    }

    public async Task<ProjectDeleteOutcome> DeleteProjectAsync(
        Guid companyId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == projectId && item.CompanyId == companyId && item.IsActive, cancellationToken);
        if (project is null)
        {
            return ProjectDeleteOutcome.NotFound;
        }

        if (project.Status != ProjectStatus.DRAFT)
        {
            return ProjectDeleteOutcome.NotDraft;
        }

        project.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return ProjectDeleteOutcome.Deleted;
    }

    /// <summary>
    /// Company submits a draft for admin review. Readiness checks (13.04): verified + active
    /// company, complete project content, at least one skill and deliverable, valid team
    /// capacity, consistent allowance fields, and a strictly future application deadline.
    /// </summary>
    public async Task<(ProjectSubmitOutcome Outcome, CompanyProjectDetailResponse? Project)> SubmitForApprovalAsync(
        Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .SingleOrDefaultAsync(company => company.Id == companyId && company.IsActive, cancellationToken);
        if (company is null)
        {
            return (ProjectSubmitOutcome.NotFound, null);
        }

        if (!company.CanPublishProjects())
        {
            return (ProjectSubmitOutcome.CompanyNotVerified, null);
        }

        var project = await dbContext.Projects
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == projectId && item.CompanyId == companyId && item.IsActive, cancellationToken);
        if (project is null)
        {
            return (ProjectSubmitOutcome.NotFound, null);
        }

        if (project.Status is not (ProjectStatus.DRAFT or ProjectStatus.CHANGES_REQUESTED))
        {
            return (ProjectSubmitOutcome.NotSubmittable, null);
        }

        if (string.IsNullOrWhiteSpace(project.Title) ||
            string.IsNullOrWhiteSpace(project.Summary) ||
            string.IsNullOrWhiteSpace(project.ProblemStatement) ||
            string.IsNullOrWhiteSpace(project.BusinessRequirements))
        {
            return (ProjectSubmitOutcome.Incomplete, null);
        }

        var skillCount = await dbContext.ProjectSkills.CountAsync(skill => skill.ProjectId == projectId, cancellationToken);
        var deliverableCount = await dbContext.ProjectDeliverables
            .CountAsync(deliverable => deliverable.ProjectId == projectId, cancellationToken);
        if (skillCount == 0 || deliverableCount == 0)
        {
            return (ProjectSubmitOutcome.Incomplete, null);
        }

        if (!IsTeamCapacityValid(project.MinTeamSize, project.MaxTeamSize, project.ExpectedStudentCount))
        {
            return (ProjectSubmitOutcome.Incomplete, null);
        }

        if (project.AllowanceAmount is > 0 && string.IsNullOrWhiteSpace(project.AllowanceCurrency))
        {
            return (ProjectSubmitOutcome.Incomplete, null);
        }

        if (!project.ApplicationDeadline.HasValue || project.ApplicationDeadline.Value <= DateTimeOffset.UtcNow)
        {
            return (ProjectSubmitOutcome.InvalidDeadline, null);
        }

        var now = DateTimeOffset.UtcNow;
        project.SubmitForApproval(userId, now);
        dbContext.ProjectApprovals.Add(new ProjectApproval(projectId, ProjectDecision.SUBMITTED, userId, now, null));
        await dbContext.SaveChangesAsync(cancellationToken);

        return (ProjectSubmitOutcome.Submitted, await ProjectDetailAsync(companyId, projectId, cancellationToken));
    }

    /// <summary>Company-side cancellation; only drafts and pending-approval projects can be cancelled by the company.</summary>
    public async Task<(ProjectCancelOutcome Outcome, CompanyProjectDetailResponse? Project)> CancelAsync(
        Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == projectId && item.CompanyId == companyId && item.IsActive, cancellationToken);
        if (project is null)
        {
            return (ProjectCancelOutcome.NotFound, null);
        }

        if (project.Status is not (ProjectStatus.DRAFT or ProjectStatus.PENDING_APPROVAL))
        {
            return (ProjectCancelOutcome.InvalidTransition, null);
        }

        var now = DateTimeOffset.UtcNow;
        project.Cancel();
        dbContext.ProjectApprovals.Add(new ProjectApproval(projectId, ProjectDecision.CANCELLED, userId, now, null));
        await dbContext.SaveChangesAsync(cancellationToken);

        return (ProjectCancelOutcome.Cancelled, await ProjectDetailAsync(companyId, projectId, cancellationToken));
    }

    /// <summary>Company reopens a rejected/changes-requested/cancelled project back into an editable DRAFT.</summary>
    public async Task<(ProjectReopenOutcome Outcome, CompanyProjectDetailResponse? Project)> ReopenAsync(
        Guid companyId, Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == projectId && item.CompanyId == companyId && item.IsActive, cancellationToken);
        if (project is null)
        {
            return (ProjectReopenOutcome.NotFound, null);
        }

        if (project.Status is not (ProjectStatus.REJECTED or ProjectStatus.CHANGES_REQUESTED or ProjectStatus.CANCELLED))
        {
            return (ProjectReopenOutcome.InvalidTransition, null);
        }

        var now = DateTimeOffset.UtcNow;
        project.Reopen();
        dbContext.ProjectApprovals.Add(new ProjectApproval(projectId, ProjectDecision.REOPENED, userId, now, null));
        await dbContext.SaveChangesAsync(cancellationToken);

        return (ProjectReopenOutcome.Reopened, await ProjectDetailAsync(companyId, projectId, cancellationToken));
    }

    /// <summary>Admin approval queue: every non-DRAFT active project, newest submission first, optional status filter.</summary>
    public async Task<PagedResponse<AdminApprovalListItemResponse>> ListApprovalsAsync(
        string? status, PageQuery query, CancellationToken cancellationToken)
    {
        var effectiveStatus = ParseStatusFilter(status);

        var baseQuery = dbContext.Projects
            .AsNoTracking()
            .Where(project => project.IsActive && project.Status != ProjectStatus.DRAFT);

        if (effectiveStatus.HasValue)
        {
            var wanted = effectiveStatus.Value;
            baseQuery = baseQuery.Where(project => project.Status == wanted);
        }

        var totalItems = await baseQuery.CountAsync(cancellationToken);

        var rows = await baseQuery
            .OrderByDescending(project => project.SubmittedAt)
            .ThenByDescending(project => project.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Join(dbContext.Companies.AsNoTracking(),
                project => project.CompanyId,
                company => company.Id,
                (project, company) => new { project, CompanyName = company.Name })
            .ToListAsync(cancellationToken);

        var items = new List<AdminApprovalListItemResponse>(rows.Count);
        foreach (var row in rows)
        {
            var lastDecision = await dbContext.ProjectApprovals
                .AsNoTracking()
                .Where(approval => approval.ProjectId == row.project.Id)
                .OrderByDescending(approval => approval.DecidedAt)
                .ThenByDescending(approval => approval.Id)
                .Select(approval => new { approval.Decision, approval.DecidedAt })
                .FirstOrDefaultAsync(cancellationToken);

            items.Add(new AdminApprovalListItemResponse(
                row.project.Id,
                row.project.Title,
                row.CompanyName,
                row.project.Status.ToString(),
                row.project.SubmittedAt,
                lastDecision?.Decision.ToString(),
                lastDecision?.DecidedAt));
        }

        return new PagedResponse<AdminApprovalListItemResponse>(
            items, PageMetadata.Create(query.Page, query.PageSize, totalItems));
    }

    /// <summary>Admin detail view with the full append-only decision history (oldest first).</summary>
    public async Task<AdminApprovalDetailResponse?> GetApprovalDetailAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == projectId && item.IsActive, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var companyName = await dbContext.Companies
            .Where(company => company.Id == project.CompanyId)
            .Select(company => company.Name)
            .SingleOrDefaultAsync(cancellationToken);

        var history = (await dbContext.ProjectApprovals
            .AsNoTracking()
            .Where(approval => approval.ProjectId == projectId)
            .OrderBy(approval => approval.DecidedAt)
            .ThenBy(approval => approval.Id)
            .ToListAsync(cancellationToken))
            .Select(approval => new ApprovalHistoryItem(
                approval.Id, approval.Decision.ToString(), approval.Note, approval.DecidedByUserId, approval.DecidedAt))
            .ToList();

        var skillCount = await dbContext.ProjectSkills.CountAsync(skill => skill.ProjectId == projectId, cancellationToken);
        var deliverableCount = await dbContext.ProjectDeliverables.CountAsync(deliverable => deliverable.ProjectId == projectId, cancellationToken);

        return new AdminApprovalDetailResponse(
            project.Id,
            project.Title,
            project.Slug,
            companyName ?? "Unknown company",
            project.Status.ToString(),
            project.Code,
            project.Summary,
            project.ProblemStatement,
            project.Difficulty.ToString(),
            project.WorkType.ToString(),
            project.DurationWeeks,
            project.ApplicationDeadline,
            project.ExpectedStudentCount,
            project.MinTeamSize,
            project.MaxTeamSize,
            project.AllowanceAmount,
            project.AllowanceCurrency,
            skillCount,
            deliverableCount,
            project.SubmittedAt,
            project.ApprovedAt,
            project.PublishedAt,
            history);
    }

    /// <summary>
    /// Applies an admin decision atomically: the state transition and the append-only
    /// history row are saved in a single transaction (read-modify-write, 13.05).
    /// </summary>
    public async Task<(AdminDecisionOutcome Outcome, AdminApprovalDetailResponse? Detail)> DecideAsync(
        Guid projectId, Guid adminUserId, ProjectDecision decision, string? note, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var project = await dbContext.Projects
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == projectId && item.IsActive, cancellationToken);
        if (project is null)
        {
            return (AdminDecisionOutcome.NotFound, null);
        }

        var now = DateTimeOffset.UtcNow;
        try
        {
            switch (decision)
            {
                case ProjectDecision.APPROVED:
                    project.Approve(adminUserId, now);
                    break;
                case ProjectDecision.CHANGES_REQUESTED:
                    project.RequestChanges();
                    break;
                case ProjectDecision.REJECTED:
                    project.Reject();
                    break;
                case ProjectDecision.SUSPENDED:
                    project.Suspend();
                    break;
                case ProjectDecision.RESUMED:
                    project.Resume();
                    break;
                default:
                    // SUBMITTED/CANCELLED/REOPENED are company-side records, never admin decisions.
                    return (AdminDecisionOutcome.InvalidTransition, null);
            }

            dbContext.ProjectApprovals.Add(new ProjectApproval(projectId, decision, adminUserId, now, note));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return (AdminDecisionOutcome.InvalidTransition, null);
        }

        return (AdminDecisionOutcome.Applied, await GetApprovalDetailAsync(projectId, cancellationToken));
    }

    /// <summary>Statuses that make a project publicly browsable (Project.IsPubliclyVisible).</summary>
    private static readonly ProjectStatus[] PubliclyVisibleStatuses =
    [
        ProjectStatus.APPROVED,
        ProjectStatus.RECRUITING,
        ProjectStatus.IN_PROGRESS,
        ProjectStatus.COMPLETED
    ];

    private static readonly string[] PublicAllowedSorts = ["newest", "oldest", "deadline", "title", "allowance"];

    /// <summary>True when the given sort key is on the public allow-list (14.03).</summary>
    public static bool IsPublicSortAllowed(string? sort) =>
        string.IsNullOrWhiteSpace(sort) ||
        PublicAllowedSorts.Contains(sort.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    /// <summary>
    /// Public browse/search endpoint. Only active projects in publicly visible statuses
    /// (APPROVED/RECRUITING/IN_PROGRESS/COMPLETED) are returned; filters combine with AND
    /// and every sort is deterministic through an Id tiebreak.
    /// </summary>
    public async Task<PagedResponse<PublicProjectListItemResponse>> SearchPublicProjectsAsync(
        PublicProjectFilterQuery query, CancellationToken cancellationToken)
    {
        var effectiveSort = string.IsNullOrWhiteSpace(query.Sort)
            ? "newest"
            : query.Sort.Trim().ToLowerInvariant();

        var baseQuery = dbContext.Projects
            .AsNoTracking()
            .Where(project => project.IsActive && PubliclyVisibleStatuses.Contains(project.Status));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            baseQuery = baseQuery.Where(project =>
                EF.Functions.ILike(project.Title, pattern) ||
                (project.Summary != null && EF.Functions.ILike(project.Summary, pattern)));
        }

        if (query.SkillId.HasValue)
        {
            var skillId = query.SkillId.Value;
            baseQuery = baseQuery.Where(project => dbContext.ProjectSkills.Any(skill =>
                skill.ProjectId == project.Id && skill.SkillId == skillId));
        }

        if (query.IndustryId.HasValue)
        {
            var industryId = query.IndustryId.Value;
            baseQuery = baseQuery.Where(project => project.IndustryId == industryId);
        }

        if (Enum.TryParse<ProjectDifficulty>(query.Difficulty?.Trim(), ignoreCase: true, out var difficulty))
        {
            baseQuery = baseQuery.Where(project => project.Difficulty == difficulty);
        }

        if (Enum.TryParse<ProjectWorkType>(query.WorkType?.Trim(), ignoreCase: true, out var workType))
        {
            baseQuery = baseQuery.Where(project => project.WorkType == workType);
        }

        if (Enum.TryParse<ProjectStatus>(query.ApplicationStatus?.Trim(), ignoreCase: true, out var status) &&
            PubliclyVisibleStatuses.Contains(status))
        {
            baseQuery = baseQuery.Where(project => project.Status == status);
        }

        if (query.DurationMin.HasValue)
        {
            baseQuery = baseQuery.Where(project => project.DurationWeeks >= query.DurationMin.Value);
        }

        if (query.DurationMax.HasValue)
        {
            baseQuery = baseQuery.Where(project => project.DurationWeeks <= query.DurationMax.Value);
        }

        if (query.AllowanceMin.HasValue)
        {
            baseQuery = baseQuery.Where(project => project.AllowanceAmount != null && project.AllowanceAmount >= query.AllowanceMin.Value);
        }

        if (query.AllowanceMax.HasValue)
        {
            baseQuery = baseQuery.Where(project => project.AllowanceAmount != null && project.AllowanceAmount <= query.AllowanceMax.Value);
        }

        var totalItems = await baseQuery.CountAsync(cancellationToken);

        var orderedQuery = effectiveSort switch
        {
            "oldest" => baseQuery.OrderBy(project => project.CreatedAt).ThenBy(project => project.Id),
            // Nulls last for deadline/allowance sorts, mirroring ListCompanyProjectsAsync.
            "deadline" => baseQuery
                .OrderBy(project => project.ApplicationDeadline == null)
                .ThenBy(project => project.ApplicationDeadline)
                .ThenBy(project => project.Id),
            "title" => baseQuery.OrderBy(project => project.NormalizedTitle).ThenBy(project => project.Id),
            "allowance" => baseQuery
                .OrderBy(project => project.AllowanceAmount == null)
                .ThenByDescending(project => project.AllowanceAmount)
                .ThenBy(project => project.Id),
            _ => baseQuery.OrderByDescending(project => project.CreatedAt).ThenByDescending(project => project.Id)
        };

        var rows = await orderedQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Join(dbContext.Companies.AsNoTracking(),
                project => project.CompanyId,
                company => company.Id,
                (project, company) => new
                {
                    project.Id,
                    project.Code,
                    project.Title,
                    project.Slug,
                    project.Summary,
                    project.Status,
                    project.Difficulty,
                    project.WorkType,
                    project.DurationWeeks,
                    project.ApplicationDeadline,
                    project.ExpectedStudentCount,
                    project.AllowanceAmount,
                    project.AllowanceCurrency,
                    CompanyName = company.Name,
                    CompanySlug = company.Slug
                })
            .ToListAsync(cancellationToken);

        var skillNamesByProject = await LoadSkillNamesAsync(rows.Select(row => row.Id), cancellationToken);

        var items = rows
            .Select(row => new PublicProjectListItemResponse(
                row.Id,
                row.Code,
                row.Title,
                row.Slug,
                row.Summary,
                row.Status.ToString(),
                row.Difficulty.ToString(),
                row.WorkType.ToString(),
                row.DurationWeeks,
                row.ApplicationDeadline,
                row.ExpectedStudentCount,
                row.AllowanceAmount,
                row.AllowanceCurrency,
                row.CompanyName,
                row.CompanySlug,
                skillNamesByProject.GetValueOrDefault(row.Id, [])))
            .ToList();

        return new PagedResponse<PublicProjectListItemResponse>(
            items, PageMetadata.Create(query.Page, query.PageSize, totalItems));
    }

    /// <summary>Public detail by slug; null when the project is missing, inactive, or not publicly visible.</summary>
    public async Task<PublicProjectDetailResponse?> GetPublicProjectBySlugAsync(
        string slug, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Include(item => item.Deliverables)
            .SingleOrDefaultAsync(item => item.Slug == slug && item.IsActive && PubliclyVisibleStatuses.Contains(item.Status), cancellationToken);
        if (project is null)
        {
            return null;
        }

        var company = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.Id == project.CompanyId)
            .Select(company => new { company.Id, company.Name, company.Slug })
            .SingleOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            return null;
        }

        string? industryName = null;
        if (project.IndustryId.HasValue)
        {
            industryName = await dbContext.Industries
                .Where(industry => industry.Id == project.IndustryId)
                .Select(industry => industry.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var skills = await LoadPublicSkillsAsync([project.Id], cancellationToken);

        var deliverables = project.Deliverables
            .OrderBy(deliverable => deliverable.SortOrder)
            .ThenBy(deliverable => deliverable.Id)
            .Select(deliverable => new PublicProjectDeliverableResponse(deliverable.Name, deliverable.Description, deliverable.SortOrder))
            .ToList();

        return new PublicProjectDetailResponse(
            project.Id,
            project.Code,
            project.Title,
            project.Slug,
            project.Summary,
            project.ProblemStatement,
            project.BusinessRequirements,
            project.TechnicalConstraints,
            project.Status.ToString(),
            project.Difficulty.ToString(),
            project.WorkType.ToString(),
            project.DurationWeeks,
            project.ApplicationDeadline,
            project.CreatedAt,
            project.MinTeamSize,
            project.MaxTeamSize,
            project.ExpectedStudentCount,
            project.AllowanceAmount,
            project.AllowanceCurrency,
            company.Id,
            company.Name,
            company.Slug,
            project.IndustryId,
            industryName,
            skills.GetValueOrDefault(project.Id, []),
            deliverables);
    }

    /// <summary>Skill requirements of one publicly visible project; null when the project is not publicly accessible.</summary>
    public async Task<IReadOnlyCollection<PublicProjectSkillResponse>?> ListPublicProjectSkillsAsync(
        Guid projectId, CancellationToken cancellationToken)
    {
        var isVisible = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(project => project.Id == projectId && project.IsActive && PubliclyVisibleStatuses.Contains(project.Status), cancellationToken);
        if (!isVisible)
        {
            return null;
        }

        var grouped = await LoadPublicSkillsAsync([projectId], cancellationToken);
        return grouped.GetValueOrDefault(projectId, []);
    }

    /// <summary>
    /// Projects sharing at least one skill with the given project, ranked by shared-skill
    /// count then newest; the source project itself and inaccessible projects are excluded.
    /// </summary>
    public async Task<IReadOnlyCollection<PublicProjectListItemResponse>?> ListRelatedProjectsAsync(
        Guid projectId, int take, CancellationToken cancellationToken)
    {
        var source = await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(project => project.Id == projectId && project.IsActive && PubliclyVisibleStatuses.Contains(project.Status), cancellationToken);
        if (source is null)
        {
            return null;
        }

        var sourceSkillIds = await dbContext.ProjectSkills
            .AsNoTracking()
            .Where(skill => skill.ProjectId == projectId)
            .Select(skill => skill.SkillId)
            .ToListAsync(cancellationToken);
        if (sourceSkillIds.Count == 0)
        {
            return [];
        }

        // One grouped query ranks candidates by shared-skill count; no navigation loading.
        var ranked = await dbContext.ProjectSkills
            .AsNoTracking()
            .Where(skill => skill.ProjectId != projectId &&
                            sourceSkillIds.Contains(skill.SkillId) &&
                            dbContext.Projects.Any(project =>
                                project.Id == skill.ProjectId &&
                                project.IsActive &&
                                PubliclyVisibleStatuses.Contains(project.Status)))
            .GroupBy(skill => skill.ProjectId)
            .Select(group => new { ProjectId = group.Key, SharedCount = group.Count() })
            .ToListAsync(cancellationToken);

        var relatedIds = ranked
            .OrderByDescending(item => item.SharedCount)
            .ThenByDescending(item => item.ProjectId)
            .Take(Math.Clamp(take, 1, 20))
            .Select(item => item.ProjectId)
            .ToList();
        if (relatedIds.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.Projects
            .AsNoTracking()
            .Where(project => relatedIds.Contains(project.Id))
            .Select(project => new
            {
                project.Id,
                project.Code,
                project.Title,
                project.Slug,
                project.Summary,
                project.Status,
                project.Difficulty,
                project.WorkType,
                project.DurationWeeks,
                project.ApplicationDeadline,
                project.ExpectedStudentCount,
                project.AllowanceAmount,
                project.AllowanceCurrency,
                project.CreatedAt,
                project.CompanyId,
                CompanyName = project.Company.Name,
                CompanySlug = project.Company.Slug
            })
            .ToListAsync(cancellationToken);

        // Preserve the ranking order computed above.
        var orderIndex = relatedIds
            .Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index);
        var ordered = rows
            .OrderBy(row => orderIndex[row.Id])
            .ToList();

        var skillNamesByProject = await LoadSkillNamesAsync(ordered.Select(row => row.Id), cancellationToken);

        return ordered
            .Select(row => new PublicProjectListItemResponse(
                row.Id,
                row.Code,
                row.Title,
                row.Slug,
                row.Summary,
                row.Status.ToString(),
                row.Difficulty.ToString(),
                row.WorkType.ToString(),
                row.DurationWeeks,
                row.ApplicationDeadline,
                row.ExpectedStudentCount,
                row.AllowanceAmount,
                row.AllowanceCurrency,
                row.CompanyName,
                row.CompanySlug,
                skillNamesByProject.GetValueOrDefault(row.Id, [])))
            .ToList();
    }

    /// <summary>Public milestones are delivered by the milestone module (Task 24); empty until then.</summary>
    public Task<IReadOnlyCollection<object>> ListPublicProjectMilestonesAsync(
        Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<object>>([]);

    /// <summary>Is the project active and publicly visible? Guards sub-resource endpoints.</summary>
    public async Task<bool> IsPubliclyAccessibleAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(project => project.Id == projectId && project.IsActive && PubliclyVisibleStatuses.Contains(project.Status), cancellationToken);

    /// <summary>
    /// One batched query resolves skill names for every project on the page (no N+1).
    /// </summary>
    private async Task<Dictionary<Guid, List<string>>> LoadSkillNamesAsync(
        IEnumerable<Guid> projectIds, CancellationToken cancellationToken)
    {
        var ids = projectIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.ProjectSkills
            .AsNoTracking()
            .Where(skill => ids.Contains(skill.ProjectId))
            .Join(dbContext.Skills,
                projectSkill => projectSkill.SkillId,
                skill => skill.Id,
                (projectSkill, skill) => new { projectSkill.ProjectId, skill.Code, skill.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(row => row.Code, StringComparer.Ordinal)
                    .Select(row => row.Name)
                    .ToList());
    }

    /// <summary>Batched skill-requirement projection keyed by project id (no N+1).</summary>
    private async Task<Dictionary<Guid, List<PublicProjectSkillResponse>>> LoadPublicSkillsAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.ProjectSkills
            .AsNoTracking()
            .Where(skill => projectIds.Contains(skill.ProjectId))
            .Join(dbContext.Skills,
                projectSkill => projectSkill.SkillId,
                skill => skill.Id,
                (projectSkill, skill) => new
                {
                    projectSkill.ProjectId,
                    projectSkill.SkillId,
                    skill.Code,
                    skill.Name,
                    projectSkill.RequirementLevel,
                    projectSkill.IsRequired
                })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(row => row.Code, StringComparer.Ordinal)
                    .Select(row => new PublicProjectSkillResponse(
                        row.SkillId, row.Name, row.RequirementLevel.ToString(), row.IsRequired))
                    .ToList());
    }

    private static ProjectStatus? ParseStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return Enum.TryParse<ProjectStatus>(status.Trim(), ignoreCase: true, out var parsed) ? parsed : null;
    }

    /// <summary>Team composition arrives with Task 17; drafts always have no team yet.</summary>
    public async Task<IReadOnlyCollection<object>?> GetProjectTeamAsync(
        Guid companyId, Guid projectId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(project => project.Id == projectId && project.CompanyId == companyId && project.IsActive, cancellationToken);
        return exists ? [] : null;
    }

    public async Task<CompanyProjectProgressResponse?> GetProjectProgressAsync(
        Guid companyId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == projectId && item.CompanyId == companyId && item.IsActive, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var deliverableCount = await dbContext.ProjectDeliverables
            .AsNoTracking()
            .CountAsync(deliverable => deliverable.ProjectId == projectId, cancellationToken);
        var skillCount = await dbContext.ProjectSkills
            .AsNoTracking()
            .CountAsync(skill => skill.ProjectId == projectId, cancellationToken);

        return new CompanyProjectProgressResponse(
            project.Id, project.Status.ToString(), deliverableCount, skillCount, project.Status == ProjectStatus.DRAFT);
    }

    private static bool IsTeamCapacityValid(int minTeamSize, int maxTeamSize, int expectedStudentCount) =>
        minTeamSize <= maxTeamSize && expectedStudentCount >= minTeamSize && expectedStudentCount <= maxTeamSize;

    /// <summary>Returns an error token when an industry or skill id is missing/inactive; null when all valid.</summary>
    private async Task<string?> ValidateCatalogAsync(Guid? industryId, IEnumerable<ProjectSkillItem> skills, CancellationToken cancellationToken)
    {
        if (industryId.HasValue &&
            !await dbContext.Industries.AnyAsync(industry => industry.Id == industryId && industry.IsActive, cancellationToken))
        {
            return "industryId";
        }

        var skillIds = skills.Select(skill => skill.SkillId).Distinct().ToList();
        if (skillIds.Count > 0)
        {
            var activeSkillCount = await dbContext.Skills
                .CountAsync(skill => skillIds.Contains(skill.Id) && skill.IsActive, cancellationToken);
            if (activeSkillCount != skillIds.Count)
            {
                return "skills";
            }
        }

        return null;
    }

    private static void ApplySkills(Project project, IEnumerable<ProjectSkillItem> skills) =>
        project.ReplaceSkills(skills
            .GroupBy(skill => skill.SkillId)
            .Select(group => (group.Key, group.First().RequirementLevel, group.First().IsRequired)));

    private static void ApplyDeliverables(Project project, IEnumerable<ProjectDeliverableItem> deliverables) =>
        project.SetDeliverables(deliverables
            .Select((deliverable, index) => (deliverable.Name, deliverable.Description,
                index + 1)));

    private async Task AssignUniqueSlugAsync(string baseSlug, CancellationToken cancellationToken, Project project)
    {
        var slug = baseSlug;
        var suffix = 1;
        while (await dbContext.Projects.AnyAsync(item => item.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{++suffix}";
        }

        project.AssignSlug(slug);
    }

    private async Task AssignUniqueCodeAsync(string baseCode, CancellationToken cancellationToken, Project project)
    {
        var code = baseCode;
        while (await dbContext.Projects.AnyAsync(item => item.Code == code, cancellationToken))
        {
            code = GenerateProjectCode();
        }

        project.AssignCode(code);
    }

    private static string GenerateProjectCode() =>
        $"PRJ-{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}"; // 8 uppercase hex chars

    private async Task<CompanyProjectDetailResponse?> ProjectDetailAsync(
        Guid companyId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Include(item => item.Deliverables)
            .SingleOrDefaultAsync(item => item.Id == projectId && item.CompanyId == companyId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        string? industryName = null;
        if (project.IndustryId.HasValue)
        {
            industryName = await dbContext.Industries
                .Where(industry => industry.Id == project.IndustryId)
                .Select(industry => industry.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var skillRows = await dbContext.ProjectSkills
            .AsNoTracking()
            .Where(skill => skill.ProjectId == projectId)
            .Join(dbContext.Skills,
                projectSkill => projectSkill.SkillId,
                skill => skill.Id,
                (projectSkill, skill) => new { projectSkill.SkillId, skill.Code, skill.Name, projectSkill.RequirementLevel, projectSkill.IsRequired })
            .ToListAsync(cancellationToken);

        var skills = skillRows
            .OrderBy(skill => skill.Code, StringComparer.Ordinal)
            .Select(skill => new ProjectSkillResponse(
                skill.SkillId, skill.Code, skill.Name, skill.RequirementLevel.ToString(), skill.IsRequired))
            .ToList();

        var deliverables = project.Deliverables
            .OrderBy(deliverable => deliverable.SortOrder)
            .ThenBy(deliverable => deliverable.Id)
            .Select(deliverable => new ProjectDeliverableResponse(deliverable.Id, deliverable.Name, deliverable.Description, deliverable.SortOrder))
            .ToList();

        return new CompanyProjectDetailResponse(
            project.Id,
            project.CompanyId,
            project.Code,
            project.Title,
            project.Slug,
            project.Status.ToString(),
            project.IsActive,
            project.Summary,
            project.ProblemStatement,
            project.BusinessRequirements,
            project.TechnicalConstraints,
            project.IndustryId,
            industryName,
            project.Difficulty,
            project.WorkType,
            project.DurationWeeks,
            project.ApplicationDeadline,
            project.ExpectedStudentCount,
            project.MinTeamSize,
            project.MaxTeamSize,
            project.AllowanceAmount,
            project.AllowanceCurrency,
            skills,
            deliverables,
            project.CreatedAt,
            project.UpdatedAt,
            project.SubmittedAt,
            project.ApprovedAt,
            project.PublishedAt);
    }
}
