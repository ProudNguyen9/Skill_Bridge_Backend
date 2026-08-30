using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Application.Catalog;

public sealed class CatalogService(ICatalogRepository catalogRepository, IOptions<CatalogOptions> catalogOptions) : ICatalogService
{
    private readonly CatalogOptions _catalogOptions = catalogOptions.Value;

    /// <summary>Lists catalog skills ordered by name. Inactive entries only appear when explicitly requested by an administrator.</summary>
    public Task<IReadOnlyCollection<SkillResponse>> GetSkillsAsync(bool includeInactive, CancellationToken cancellationToken) =>
        catalogRepository.GetSkillsAsync(includeInactive, cancellationToken);

    /// <summary>Lists catalog industries ordered by name.</summary>
    public Task<IReadOnlyCollection<IndustryResponse>> GetIndustriesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        catalogRepository.GetIndustriesAsync(includeInactive, cancellationToken);

    /// <summary>Lists catalog faculties ordered by name.</summary>
    public Task<IReadOnlyCollection<FacultyResponse>> GetFacultiesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        catalogRepository.GetFacultiesAsync(includeInactive, cancellationToken);

    /// <summary>Lists catalog majors ordered by faculty and name, optionally restricted to one faculty.</summary>
    public Task<IReadOnlyCollection<MajorResponse>> GetMajorsAsync(Guid? facultyId, bool includeInactive, CancellationToken cancellationToken) =>
        catalogRepository.GetMajorsAsync(facultyId, includeInactive, cancellationToken);

    /// <summary>Lists supported banks with their BIN codes, ordered by name.</summary>
    public Task<IReadOnlyCollection<BankResponse>> GetBanksAsync(bool includeInactive, CancellationToken cancellationToken) =>
        catalogRepository.GetBanksAsync(includeInactive, cancellationToken);

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
