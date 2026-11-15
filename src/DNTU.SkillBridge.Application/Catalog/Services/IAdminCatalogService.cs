using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Administration;
using DNTU.SkillBridge.Domain.Catalog;

namespace DNTU.SkillBridge.Application.Catalog;

public interface IAdminCatalogService
{
    Task<AdminCatalogWriteResult<SkillResponse>> CreateSkillAsync(Guid actorUserId, CreateSkillRequest request, CancellationToken cancellationToken);
    Task<AdminCatalogWriteResult<SkillResponse>> UpdateSkillAsync(Guid actorUserId, Guid skillId, UpdateSkillRequest request, CancellationToken cancellationToken);
    Task<AdminCatalogWriteResult<SkillResponse>> SetSkillActiveAsync(Guid actorUserId, Guid skillId, bool isActive, CancellationToken cancellationToken);

    Task<AdminCatalogWriteResult<IndustryResponse>> CreateIndustryAsync(Guid actorUserId, CreateIndustryRequest request, CancellationToken cancellationToken);
    Task<AdminCatalogWriteResult<IndustryResponse>> SetIndustryActiveAsync(Guid actorUserId, Guid industryId, bool isActive, CancellationToken cancellationToken);

    Task<AdminCatalogWriteResult<FacultyResponse>> CreateFacultyAsync(Guid actorUserId, CreateFacultyRequest request, CancellationToken cancellationToken);
    Task<AdminCatalogWriteResult<FacultyResponse>> SetFacultyActiveAsync(Guid actorUserId, Guid facultyId, bool isActive, CancellationToken cancellationToken);

    Task<AdminCatalogWriteResult<MajorResponse>> CreateMajorAsync(Guid actorUserId, CreateMajorRequest request, CancellationToken cancellationToken);
    Task<AdminCatalogWriteResult<MajorResponse>> SetMajorActiveAsync(Guid actorUserId, Guid majorId, bool isActive, CancellationToken cancellationToken);
}

/// <summary>
/// Quản trị danh mục hệ thống (kỹ năng, ngành nghề, khoa, bộ môn): tạo mới, cập nhật và bật/tắt hoạt động.
/// Mỗi thay đổi đều ghi AuditLog để truy vết.
/// </summary>
public sealed class AdminCatalogService(ICatalogRepository catalogRepository, IUnitOfWork unitOfWork) : IAdminCatalogService
{
    public async Task<AdminCatalogWriteResult<SkillResponse>> CreateSkillAsync(Guid actorUserId, CreateSkillRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        if (code.Length == 0 || name.Length == 0)
        {
            return new AdminCatalogWriteResult<SkillResponse>(AdminCatalogWriteStatus.Duplicate, null);
        }

        if (await catalogRepository.SkillCodeOrNameExistsAsync(code, name.ToUpperInvariant(), cancellationToken))
        {
            return new AdminCatalogWriteResult<SkillResponse>(AdminCatalogWriteStatus.Duplicate, null);
        }

        var skill = new Skill(code, name, ParseCategory(request.Category), request.Description);
        catalogRepository.AddSkill(skill);
        LogAudit(catalogRepository, actorUserId, AuditActions.CatalogEntryCreated, nameof(Skill), skill.Id, skill.Name);
await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AdminCatalogWriteResult<SkillResponse>(AdminCatalogWriteStatus.Created, MapSkill(skill));
    }

    public async Task<AdminCatalogWriteResult<SkillResponse>> UpdateSkillAsync(Guid actorUserId, Guid skillId, UpdateSkillRequest request, CancellationToken cancellationToken)
    {
        var skill = await catalogRepository.FindSkillAsync(skillId, cancellationToken);
        if (skill is null)
        {
            return new AdminCatalogWriteResult<SkillResponse>(AdminCatalogWriteStatus.NotFound, null);
        }

        var name = request.Name.Trim();
        var newNameNormalized = name.ToUpperInvariant();
        if (await catalogRepository.SkillNameExistsAsync(newNameNormalized, cancellationToken) &&
            !string.Equals(skill.NormalizedName, newNameNormalized, StringComparison.Ordinal))
        {
            return new AdminCatalogWriteResult<SkillResponse>(AdminCatalogWriteStatus.Duplicate, null);
        }

        skill.Update(name, ParseCategory(request.Category), request.Description);
        LogAudit(catalogRepository, actorUserId, AuditActions.CatalogEntryUpdated, nameof(Skill), skill.Id, skill.Name);
await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AdminCatalogWriteResult<SkillResponse>(AdminCatalogWriteStatus.Updated, MapSkill(skill));
    }

    public async Task<AdminCatalogWriteResult<SkillResponse>> SetSkillActiveAsync(Guid actorUserId, Guid skillId, bool isActive, CancellationToken cancellationToken)
    {
        var skill = await catalogRepository.FindSkillAsync(skillId, cancellationToken);
        if (skill is null)
        {
            return new AdminCatalogWriteResult<SkillResponse>(AdminCatalogWriteStatus.NotFound, null);
        }

        if (isActive) skill.Activate(); else skill.Deactivate();
        LogAudit(catalogRepository, actorUserId, AuditActions.CatalogEntryRemoved, nameof(Skill), skill.Id, skill.Name);
await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AdminCatalogWriteResult<SkillResponse>(AdminCatalogWriteStatus.Updated, MapSkill(skill));
    }

    public async Task<AdminCatalogWriteResult<IndustryResponse>> CreateIndustryAsync(Guid actorUserId, CreateIndustryRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0 || await catalogRepository.IndustryNameExistsAsync(name.ToUpperInvariant(), cancellationToken))
        {
            return new AdminCatalogWriteResult<IndustryResponse>(AdminCatalogWriteStatus.Duplicate, null);
        }

        var industry = new Industry(name);
        catalogRepository.AddIndustry(industry);
        LogAudit(catalogRepository, actorUserId, AuditActions.CatalogEntryCreated, nameof(Industry), industry.Id, industry.Name);
await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AdminCatalogWriteResult<IndustryResponse>(AdminCatalogWriteStatus.Created,
            new IndustryResponse(industry.Id, industry.Name, industry.Slug, industry.IsActive));
    }

    public async Task<AdminCatalogWriteResult<IndustryResponse>> SetIndustryActiveAsync(Guid actorUserId, Guid industryId, bool isActive, CancellationToken cancellationToken)
    {
        var industry = await catalogRepository.FindIndustryAsync(industryId, cancellationToken);
        if (industry is null)
        {
            return new AdminCatalogWriteResult<IndustryResponse>(AdminCatalogWriteStatus.NotFound, null);
        }

        if (isActive) industry.Activate(); else industry.Deactivate();
        LogAudit(catalogRepository, actorUserId, AuditActions.CatalogEntryUpdated, nameof(Industry), industry.Id, industry.Name);
await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AdminCatalogWriteResult<IndustryResponse>(AdminCatalogWriteStatus.Updated,
            new IndustryResponse(industry.Id, industry.Name, industry.Slug, industry.IsActive));
    }

    public async Task<AdminCatalogWriteResult<FacultyResponse>> CreateFacultyAsync(Guid actorUserId, CreateFacultyRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        if (code.Length == 0 || name.Length == 0 || await catalogRepository.FacultyCodeExistsAsync(code, cancellationToken))
        {
            return new AdminCatalogWriteResult<FacultyResponse>(AdminCatalogWriteStatus.Duplicate, null);
        }

        var faculty = new Faculty(code, name);
        catalogRepository.AddFaculty(faculty);
        LogAudit(catalogRepository, actorUserId, AuditActions.CatalogEntryCreated, nameof(Faculty), faculty.Id, faculty.Name);
await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AdminCatalogWriteResult<FacultyResponse>(AdminCatalogWriteStatus.Created,
            new FacultyResponse(faculty.Id, faculty.Code, faculty.Name, faculty.IsActive));
    }

    public async Task<AdminCatalogWriteResult<FacultyResponse>> SetFacultyActiveAsync(Guid actorUserId, Guid facultyId, bool isActive, CancellationToken cancellationToken)
    {
        var faculty = await catalogRepository.FindFacultyAsync(facultyId, cancellationToken);
        if (faculty is null)
        {
            return new AdminCatalogWriteResult<FacultyResponse>(AdminCatalogWriteStatus.NotFound, null);
        }

        if (isActive) faculty.Activate(); else faculty.Deactivate();
        LogAudit(catalogRepository, actorUserId, AuditActions.CatalogEntryUpdated, nameof(Faculty), faculty.Id, faculty.Name);
await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AdminCatalogWriteResult<FacultyResponse>(AdminCatalogWriteStatus.Updated,
            new FacultyResponse(faculty.Id, faculty.Code, faculty.Name, faculty.IsActive));
    }

    public async Task<AdminCatalogWriteResult<MajorResponse>> CreateMajorAsync(Guid actorUserId, CreateMajorRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        var faculty = request.FacultyId == Guid.Empty
            ? null
            : await catalogRepository.FindFacultyAsync(request.FacultyId, cancellationToken);
        if (faculty is null || !faculty.IsActive)
        {
            return new AdminCatalogWriteResult<MajorResponse>(AdminCatalogWriteStatus.FacultyMissing, null);
        }

        if (code.Length == 0 || name.Length == 0 || await catalogRepository.MajorCodeExistsAsync(code, cancellationToken))
        {
            return new AdminCatalogWriteResult<MajorResponse>(AdminCatalogWriteStatus.Duplicate, null);
        }

        var major = new Major(code, name, faculty.Id);
        catalogRepository.AddMajor(major);
        LogAudit(catalogRepository, actorUserId, AuditActions.CatalogEntryCreated, nameof(Major), major.Id, major.Name);
await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AdminCatalogWriteResult<MajorResponse>(AdminCatalogWriteStatus.Created,
            new MajorResponse(major.Id, major.Code, major.Name, major.FacultyId, faculty.Name, major.IsActive));
    }

    public async Task<AdminCatalogWriteResult<MajorResponse>> SetMajorActiveAsync(Guid actorUserId, Guid majorId, bool isActive, CancellationToken cancellationToken)
    {
        var major = await catalogRepository.FindMajorAsync(majorId, cancellationToken);
        if (major is null)
        {
            return new AdminCatalogWriteResult<MajorResponse>(AdminCatalogWriteStatus.NotFound, null);
        }

        if (isActive) major.Activate(); else major.Deactivate();
        LogAudit(catalogRepository, actorUserId, AuditActions.CatalogEntryUpdated, nameof(Major), major.Id, major.Name);
await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AdminCatalogWriteResult<MajorResponse>(AdminCatalogWriteStatus.Updated,
            new MajorResponse(major.Id, major.Code, major.Name, major.FacultyId, major.Faculty.Name, major.IsActive));
    }

    private static SkillCategory ParseCategory(string category)
    {
        return Enum.TryParse<SkillCategory>(category.Trim(), ignoreCase: true, out var parsed) ? parsed : SkillCategory.TECHNICAL;
    }

    private static SkillResponse MapSkill(Skill skill) =>
        new(skill.Id, skill.Code, skill.Name, skill.Category.ToString(), skill.Description, skill.IsActive);

    private static void LogAudit(ICatalogRepository repository, Guid actorUserId, string action, string entityType, Guid entityId, string name)
    {
        var metadataJson = System.Text.Json.JsonSerializer.Serialize(new { name });
        repository.AddAuditLog(new AuditLog(actorUserId, action, entityType, entityId.ToString(), null, null, null, null, null, metadataJson, DateTimeOffset.UtcNow));
    }
}
