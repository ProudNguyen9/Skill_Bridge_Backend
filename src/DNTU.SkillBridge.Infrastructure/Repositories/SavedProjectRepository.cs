using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Students;
using DNTU.SkillBridge.Domain.Catalog;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the saved-project data operations.</summary>
public sealed class SavedProjectRepository(AppDbContext dbContext) : ISavedProjectRepository
{
    /// <summary>Statuses that make a project publicly visible (Project.IsPubliclyVisible).</summary>
    private static readonly ProjectStatus[] PubliclyVisibleStatuses =
    [
        ProjectStatus.APPROVED,
        ProjectStatus.RECRUITING,
        ProjectStatus.IN_PROGRESS,
        ProjectStatus.COMPLETED
    ];

    public Task<Guid?> FindStudentProfileIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public void AddStudentProfile(StudentProfile profile) => dbContext.StudentProfiles.Add(profile);

    public async Task<(bool IsActive, ProjectStatus Status)?> FindProjectVisibilityAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var projectStatus = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new { project.IsActive, project.Status })
            .SingleOrDefaultAsync(cancellationToken);
        return projectStatus is null ? null : (projectStatus.IsActive, projectStatus.Status);
    }

    public Task<bool> IsProjectPubliclyVisibleAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects
            .AsNoTracking()
            .AnyAsync(project => project.Id == projectId && project.IsActive && PubliclyVisibleStatuses.Contains(project.Status), cancellationToken);

    public Task<bool> HasSavedAsync(Guid studentId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.SavedProjects
            .AsNoTracking()
            .AnyAsync(saved => saved.StudentId == studentId && saved.ProjectId == projectId, cancellationToken);

    public void AddSavedProject(SavedProject savedProject) => dbContext.SavedProjects.Add(savedProject);

    public Task<SavedProject?> FindSavedAsync(Guid studentId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.SavedProjects
            .SingleOrDefaultAsync(item => item.StudentId == studentId && item.ProjectId == projectId, cancellationToken);

    public void RemoveSavedProject(SavedProject savedProject) => dbContext.SavedProjects.Remove(savedProject);

    public async Task<(IReadOnlyCollection<SavedProjectListItemResponse> Items, int TotalItems)> ListSavedProjectsAsync(Guid studentId, PageQuery query, CancellationToken cancellationToken)
    {
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

        return (items, totalItems);
    }

    public async Task<IReadOnlyCollection<(Guid SkillId, string Code, string Name)>> ListRequiredSkillsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.ProjectSkills
            .AsNoTracking()
            .Where(projectSkill => projectSkill.ProjectId == projectId)
            .Join(dbContext.Skills,
                projectSkill => projectSkill.SkillId,
                skill => skill.Id,
                (projectSkill, skill) => new { projectSkill.SkillId, skill.Code, skill.Name })
            .ToListAsync(cancellationToken);
        return rows.Select(row => (row.SkillId, row.Code, row.Name)).ToList();
    }

    public async Task<IReadOnlyCollection<Guid>> ListDeclaredActiveSkillIdsAsync(Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.StudentSkills
            .AsNoTracking()
            .Where(studentSkill => studentSkill.StudentId == studentId)
            .Join(dbContext.Skills.Where(skill => skill.IsActive),
                studentSkill => studentSkill.SkillId,
                skill => skill.Id,
                (studentSkill, skill) => studentSkill.SkillId)
            .ToListAsync(cancellationToken);
}
