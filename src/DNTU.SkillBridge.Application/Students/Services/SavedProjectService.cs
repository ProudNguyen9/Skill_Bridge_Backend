using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Students;

namespace DNTU.SkillBridge.Application.Students;

/// <summary>
/// Student-scoped saved projects and the informational skill match. The student profile
/// is always resolved server-side from the authenticated userId (lazily created like
/// StudentService), never from a payload; the list only ever reads the caller's own rows.
/// </summary>
public sealed class SavedProjectService(ISavedProjectRepository savedProjectRepository, IUnitOfWork unitOfWork) : ISavedProjectService
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

        var projectStatus = await savedProjectRepository.FindProjectVisibilityAsync(projectId, cancellationToken);
        if (projectStatus is null || !projectStatus.Value.IsActive)
        {
            return SavedProjectOutcome.NotFound;
        }

        if (!PubliclyVisibleStatuses.Contains(projectStatus.Value.Status))
        {
            return SavedProjectOutcome.ProjectNotVisible;
        }

        if (await savedProjectRepository.HasSavedAsync(studentId, projectId, cancellationToken))
        {
            return SavedProjectOutcome.AlreadySaved;
        }

        savedProjectRepository.AddSavedProject(new SavedProject(studentId, projectId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SavedProjectOutcome.Saved;
    }

    /// <summary>Removes the caller's own bookmark; rows of other students are never touched. True when removed.</summary>
    public async Task<bool> UnsaveProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveProfileIdAsync(userId, cancellationToken);

        // Removal works even when the project has become invisible, so students can clean up their list.
        var saved = await savedProjectRepository.FindSavedAsync(studentId, projectId, cancellationToken);
        if (saved is null)
        {
            return false;
        }

        savedProjectRepository.RemoveSavedProject(saved);
        await unitOfWork.SaveChangesAsync(cancellationToken);
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

        var (items, totalItems) = await savedProjectRepository.ListSavedProjectsAsync(studentId, query, cancellationToken);

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
        if (!await savedProjectRepository.IsProjectPubliclyVisibleAsync(projectId, cancellationToken))
        {
            return null;
        }

        var studentId = await ResolveProfileIdAsync(userId, cancellationToken);

        // In-memory ordering: StringComparer.Ordinal does not translate to SQL.
        var required = (await savedProjectRepository.ListRequiredSkillsAsync(projectId, cancellationToken))
            .OrderBy(row => row.Code, StringComparer.Ordinal).ToList();

        var declaredSkillIds = (await savedProjectRepository.ListDeclaredActiveSkillIdsAsync(studentId, cancellationToken))
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
        var profileId = await savedProjectRepository.FindStudentProfileIdAsync(userId, cancellationToken);
        if (profileId.HasValue)
        {
            return profileId.Value;
        }

        var profile = new StudentProfile(userId);
        savedProjectRepository.AddStudentProfile(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile.Id;
    }
}
