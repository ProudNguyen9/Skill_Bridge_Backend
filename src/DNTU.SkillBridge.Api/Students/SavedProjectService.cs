using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Students;

public enum SavedProjectOutcome
{
    Saved,
    AlreadySaved,
    NotFound,
    ProjectNotVisible
}

/// <summary>
/// Student-scoped saved projects and the informational skill match. The student profile
/// is always resolved server-side from the authenticated userId (lazily created like
/// StudentService), never from a payload; the list only ever reads the caller's own rows.
/// </summary>
public sealed class SavedProjectService(AppDbContext dbContext)
{
    /// <summary>Statuses that make a project publicly visible (Project.IsPubliclyVisible).</summary>
    private static readonly ProjectStatus[] PubliclyVisibleStatuses =
    [
        ProjectStatus.APPROVED,
        ProjectStatus.RECRUITING,
        ProjectStatus.IN_PROGRESS,
        ProjectStatus.COMPLETED
    ];

    /// <summary>
    /// Saves a publicly visible project. Idempotent: a second save answers AlreadySaved
    /// without writing. Drafts, hidden, and inactive projects answer NotFound/ProjectNotVisible.
    /// </summary>
    public async Task<SavedProjectOutcome> SaveProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveProfileIdAsync(userId, cancellationToken);

        var projectStatus = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new { project.IsActive, project.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (projectStatus is null || !projectStatus.IsActive)
        {
            return SavedProjectOutcome.NotFound;
        }

        if (!PubliclyVisibleStatuses.Contains(projectStatus.Status))
        {
            return SavedProjectOutcome.ProjectNotVisible;
        }

        var alreadySaved = await dbContext.SavedProjects
            .AsNoTracking()
            .AnyAsync(saved => saved.StudentId == studentId && saved.ProjectId == projectId, cancellationToken);
        if (alreadySaved)
        {
            return SavedProjectOutcome.AlreadySaved;
        }

        dbContext.SavedProjects.Add(new SavedProject(studentId, projectId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return SavedProjectOutcome.Saved;
    }

    /// <summary>Removes the caller's own bookmark; rows of other students are never touched. True when removed.</summary>
    public async Task<bool> UnsaveProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveProfileIdAsync(userId, cancellationToken);

        // Removal works even when the project has become invisible, so students can clean up their list.
        var saved = await dbContext.SavedProjects
            .SingleOrDefaultAsync(item => item.StudentId == studentId && item.ProjectId == projectId, cancellationToken);
        if (saved is null)
        {
            return false;
        }

        dbContext.SavedProjects.Remove(saved);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The caller's saved projects, newest save first. Projects that have since become
    /// inactive or hidden disappear from the list (documented UX policy) but the bookmark
    /// stays in the database so re-publication restores the row.
    /// </summary>
    public async Task<PagedResponse<SavedProjectListItemResponse>> ListSavedProjectsAsync(
        Guid userId, PageQuery query, CancellationToken cancellationToken)
    {
        var studentId = await ResolveProfileIdAsync(userId, cancellationToken);

        var baseQuery = dbContext.SavedProjects
            .AsNoTracking()
            .Where(saved => saved.StudentId == studentId)
            .Join(dbContext.Projects.AsNoTracking(),
                saved => saved.ProjectId,
                project => project.Id,
                (saved, project) => new { saved, project })
            .Where(row => row.project.IsActive && PubliclyVisibleStatuses.Contains(row.project.Status));

        var totalItems = await baseQuery.CountAsync(cancellationToken);

        var rows = await baseQuery
            .OrderByDescending(row => row.saved.CreatedAt)
            .ThenByDescending(row => row.saved.ProjectId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new
            {
                row.project.Id,
                row.project.Title,
                row.project.Slug,
                row.project.Status,
                row.project.Difficulty,
                row.project.DurationWeeks,
                row.project.AllowanceAmount,
                row.project.AllowanceCurrency,
                CompanyName = row.project.Company.Name,
                SavedAt = row.saved.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new SavedProjectListItemResponse(
                row.Id,
                row.Title,
                row.Slug,
                row.CompanyName,
                row.Status.ToString(),
                row.Difficulty.ToString(),
                row.DurationWeeks,
                row.AllowanceAmount,
                row.AllowanceCurrency,
                row.SavedAt))
            .ToList();

        return new PagedResponse<SavedProjectListItemResponse>(
            items, PageMetadata.Create(query.Page, query.PageSize, totalItems));
    }

    /// <summary>
    /// Deterministic match between the caller's active declared skills and the project's
    /// skill requirements. Null when the project is not publicly visible. Match is
    /// informational and never gates applications.
    /// </summary>
    public async Task<SkillMatchResponse?> MatchSkillsWithProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var isVisible = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(project => project.Id == projectId && project.IsActive && PubliclyVisibleStatuses.Contains(project.Status), cancellationToken);
        if (!isVisible)
        {
            return null;
        }

        var studentId = await ResolveProfileIdAsync(userId, cancellationToken);

        var requiredRows = await dbContext.ProjectSkills
            .AsNoTracking()
            .Where(projectSkill => projectSkill.ProjectId == projectId)
            .Join(dbContext.Skills,
                projectSkill => projectSkill.SkillId,
                skill => skill.Id,
                (projectSkill, skill) => new { projectSkill.SkillId, skill.Code, skill.Name })
            .ToListAsync(cancellationToken);
        // In-memory ordering: StringComparer.Ordinal does not translate to SQL.
        var required = requiredRows.OrderBy(row => row.Code, StringComparer.Ordinal).ToList();

        var declaredSkillIds = (await dbContext.StudentSkills
            .AsNoTracking()
            .Where(studentSkill => studentSkill.StudentId == studentId)
            .Join(dbContext.Skills.Where(skill => skill.IsActive),
                studentSkill => studentSkill.SkillId,
                skill => skill.Id,
                (studentSkill, skill) => studentSkill.SkillId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var matched = required
            .Where(row => declaredSkillIds.Contains(row.SkillId))
            .Select(row => new SkillMatchItemResponse(row.SkillId, row.Name))
            .ToList();
        var missing = required
            .Where(row => !declaredSkillIds.Contains(row.SkillId))
            .Select(row => new SkillMatchItemResponse(row.SkillId, row.Name))
            .ToList();

        var requiredCount = required.Count;
        var matchPercent = requiredCount == 0 ? 0 : matched.Count * 100 / requiredCount;

        return new SkillMatchResponse(projectId, requiredCount, matched.Count, matchPercent, matched, missing);
    }

    /// <summary>
    /// Resolves the student profile id from the authenticated userId, lazily creating the
    /// profile with default privacy settings on first access (same semantics as StudentService).
    /// </summary>
    private async Task<Guid> ResolveProfileIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profileId = await dbContext.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (profileId.HasValue)
        {
            return profileId.Value;
        }

        var profile = new StudentProfile(userId);
        dbContext.StudentProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile.Id;
    }
}
