using DNTU.SkillBridge.Application.Catalog;
using DNTU.SkillBridge.Domain.Administration;
using DNTU.SkillBridge.Domain.Catalog;

namespace DNTU.SkillBridge.Application.Catalog;

/// <summary>
/// Catalog queries plus the admin write operations. Read endpoints are anonymous;
/// writes are only reachable from the admin catalog controller.
/// </summary>
public interface ICatalogRepository
{
    Task<IReadOnlyCollection<SkillResponse>> GetSkillsAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<IndustryResponse>> GetIndustriesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<FacultyResponse>> GetFacultiesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MajorResponse>> GetMajorsAsync(Guid? facultyId, bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BankResponse>> GetBanksAsync(bool includeInactive, CancellationToken cancellationToken);

    // Tracked finds for admin mutations
    Task<Skill?> FindSkillAsync(Guid id, CancellationToken cancellationToken);
    Task<Industry?> FindIndustryAsync(Guid id, CancellationToken cancellationToken);
    Task<Faculty?> FindFacultyAsync(Guid id, CancellationToken cancellationToken);
    Task<Major?> FindMajorAsync(Guid id, CancellationToken cancellationToken);
    Task<Faculty?> FindFacultyForUpdateAsync(Guid id, CancellationToken cancellationToken);

    // Existence probes for duplicate validation
    Task<bool> SkillCodeOrNameExistsAsync(string code, string normalizedName, CancellationToken cancellationToken);
    Task<bool> SkillNameExistsAsync(string normalizedName, CancellationToken cancellationToken);
    Task<bool> IndustryNameExistsAsync(string normalizedName, CancellationToken cancellationToken);
    Task<bool> FacultyCodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<bool> MajorCodeExistsAsync(string code, CancellationToken cancellationToken);

    void AddSkill(Skill skill);
    void AddIndustry(Industry industry);
    void AddFaculty(Faculty faculty);
    void AddMajor(Major major);

    void AddAuditLog(AuditLog auditLog);
}
