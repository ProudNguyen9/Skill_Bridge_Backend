namespace DNTU.SkillBridge.Api.Catalog;

/// <summary>A skill entry returned by the catalog endpoints.</summary>
public sealed record SkillResponse(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string? Description,
    bool IsActive);

/// <summary>An industry entry returned by the catalog endpoints.</summary>
public sealed record IndustryResponse(Guid Id, string Name, string Slug, bool IsActive);

/// <summary>A faculty entry returned by the catalog endpoints.</summary>
public sealed record FacultyResponse(Guid Id, string Code, string Name, bool IsActive);

/// <summary>A major entry, always tied to its faculty, returned by the catalog endpoints.</summary>
public sealed record MajorResponse(Guid Id, string Code, string Name, Guid FacultyId, string FacultyName, bool IsActive);

/// <summary>A bank entry with its Vietnamese BIN code, returned by the catalog endpoints.</summary>
public sealed record BankResponse(Guid Id, string Code, string Name, string Bin, bool IsActive);

/// <summary>Inclusive numeric bounds exposed through the project metadata endpoint.</summary>
public sealed record CatalogRangeMetadata(int Min, int Max);

/// <summary>Allowance configuration exposed through the project metadata endpoint.</summary>
public sealed record CatalogAllowanceMetadata(string Currency, CatalogRangeMetadata Amount);

/// <summary>Configuration-backed metadata for project definitions (difficulty, work type, duration, team size, allowance).</summary>
public sealed record ProjectMetadataResponse(
    IReadOnlyCollection<string> Difficulties,
    IReadOnlyCollection<string> WorkTypes,
    CatalogRangeMetadata DurationWeeks,
    CatalogRangeMetadata TeamSize,
    CatalogAllowanceMetadata Allowance);

/// <summary>Configuration-backed metadata for kanban tasks (statuses and priorities).</summary>
public sealed record TaskMetadataResponse(IReadOnlyCollection<string> Statuses, IReadOnlyCollection<string> Priorities);

public sealed class CatalogListQuery
{
    /// <summary>When true, inactive entries are included. Requires the ADMIN or SUPER_ADMIN role.</summary>
    public bool IncludeInactive { get; init; }
}

public sealed class MajorListQuery
{
    /// <summary>Optionally restricts majors to a single faculty.</summary>
    public Guid? FacultyId { get; init; }

    /// <summary>When true, inactive entries are included. Requires the ADMIN or SUPER_ADMIN role.</summary>
    public bool IncludeInactive { get; init; }
}
