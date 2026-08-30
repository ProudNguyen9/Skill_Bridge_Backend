using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Application.Catalog;

public interface ICatalogService
{
    Task<IReadOnlyCollection<SkillResponse>> GetSkillsAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<IndustryResponse>> GetIndustriesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<FacultyResponse>> GetFacultiesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MajorResponse>> GetMajorsAsync(Guid? facultyId, bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BankResponse>> GetBanksAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<ProjectMetadataResponse> GetProjectMetadataAsync(CancellationToken cancellationToken);

    Task<TaskMetadataResponse> GetTaskMetadataAsync(CancellationToken cancellationToken);
}
