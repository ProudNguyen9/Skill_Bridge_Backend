using DNTU.SkillBridge.Application.Catalog;

namespace DNTU.SkillBridge.Application.Catalog;

/// <summary>
/// Read-side catalog queries. Catalog entries are read-only through the API,
/// so the repository only exposes projected list queries.
/// </summary>
public interface ICatalogRepository
{
    Task<IReadOnlyCollection<SkillResponse>> GetSkillsAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<IndustryResponse>> GetIndustriesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<FacultyResponse>> GetFacultiesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MajorResponse>> GetMajorsAsync(Guid? facultyId, bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BankResponse>> GetBanksAsync(bool includeInactive, CancellationToken cancellationToken);
}
