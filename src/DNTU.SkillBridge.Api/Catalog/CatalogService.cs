using DNTU.SkillBridge.Api.Configuration;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Api.Catalog;

public sealed class CatalogService(AppDbContext dbContext, IOptions<CatalogOptions> catalogOptions)
{
    private readonly CatalogOptions _catalogOptions = catalogOptions.Value;

    /// <summary>Lists catalog skills ordered by name. Inactive entries only appear when explicitly requested by an administrator.</summary>
    public async Task<IReadOnlyCollection<SkillResponse>> GetSkillsAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Skills
            .AsNoTracking()
            .Where(skill => includeInactive || skill.IsActive)
            .OrderBy(skill => skill.Name)
            .ThenBy(skill => skill.Code)
            .Select(skill => new { skill.Id, skill.Code, skill.Name, skill.Category, skill.Description, skill.IsActive })
            .ToListAsync(cancellationToken);

        return rows
            .Select(skill => new SkillResponse(
                skill.Id,
                skill.Code,
                skill.Name,
                skill.Category.ToString(),
                skill.Description,
                skill.IsActive))
            .ToList();
    }

    /// <summary>Lists catalog industries ordered by name.</summary>
    public async Task<IReadOnlyCollection<IndustryResponse>> GetIndustriesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        return await dbContext.Industries
            .AsNoTracking()
            .Where(industry => includeInactive || industry.IsActive)
            .OrderBy(industry => industry.Name)
            .Select(industry => new IndustryResponse(industry.Id, industry.Name, industry.Slug, industry.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Lists catalog faculties ordered by name.</summary>
    public async Task<IReadOnlyCollection<FacultyResponse>> GetFacultiesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        return await dbContext.Faculties
            .AsNoTracking()
            .Where(faculty => includeInactive || faculty.IsActive)
            .OrderBy(faculty => faculty.Name)
            .Select(faculty => new FacultyResponse(faculty.Id, faculty.Code, faculty.Name, faculty.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Lists catalog majors ordered by faculty and name, optionally restricted to one faculty.</summary>
    public async Task<IReadOnlyCollection<MajorResponse>> GetMajorsAsync(Guid? facultyId, bool includeInactive, CancellationToken cancellationToken)
    {
        return await dbContext.Majors
            .AsNoTracking()
            .Where(major => (includeInactive || major.IsActive) && (!facultyId.HasValue || major.FacultyId == facultyId))
            .OrderBy(major => major.Faculty.Name)
            .ThenBy(major => major.Name)
            .Select(major => new MajorResponse(
                major.Id,
                major.Code,
                major.Name,
                major.FacultyId,
                major.Faculty.Name,
                major.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Lists supported banks with their BIN codes, ordered by name.</summary>
    public async Task<IReadOnlyCollection<BankResponse>> GetBanksAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        return await dbContext.Banks
            .AsNoTracking()
            .Where(bank => includeInactive || bank.IsActive)
            .OrderBy(bank => bank.Name)
            .ThenBy(bank => bank.Code)
            .Select(bank => new BankResponse(bank.Id, bank.Code, bank.Name, bank.Bin, bank.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Returns configuration-backed project metadata used to build project forms and filters.</summary>
    public Task<ProjectMetadataResponse> GetProjectMetadataAsync(CancellationToken cancellationToken)
    {
        var project = _catalogOptions.Project;
        return Task.FromResult(new ProjectMetadataResponse(
            project.Difficulties,
            project.WorkTypes,
            new CatalogRangeMetadata(project.DurationWeeks.Min, project.DurationWeeks.Max),
            new CatalogRangeMetadata(project.TeamSize.Min, project.TeamSize.Max),
            new CatalogAllowanceMetadata(
                project.Allowance.Currency,
                new CatalogRangeMetadata(project.Allowance.Amount.Min, project.Allowance.Amount.Max))));
    }

    /// <summary>Returns configuration-backed task metadata (kanban statuses and priorities).</summary>
    public Task<TaskMetadataResponse> GetTaskMetadataAsync(CancellationToken cancellationToken)
    {
        var task = _catalogOptions.Task;
        return Task.FromResult(new TaskMetadataResponse(task.Statuses, task.Priorities));
    }

    /// <summary>Determines whether the current user may request inactive catalog entries.</summary>
    public static bool CanIncludeInactive(ICurrentUser currentUser) =>
        currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin);
}
