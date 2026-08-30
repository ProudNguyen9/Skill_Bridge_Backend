using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Catalog;

public sealed class Skill : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public SkillCategory Category { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Skill() { }

    public Skill(string code, string name, SkillCategory category, string? description = null)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Slug = SlugBuilder.Create(Name);
        Category = category;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

public sealed class Industry : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private Industry() { }

    public Industry(string name)
    {
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Slug = SlugBuilder.Create(Name);
    }

    public void Deactivate() => IsActive = false;
}

public sealed class Faculty : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private Faculty() { }

    public Faculty(string code, string name)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
    }

    public void Deactivate() => IsActive = false;
}

public sealed class Major : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public Guid FacultyId { get; private set; }
    public Faculty Faculty { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    private Major() { }

    public Major(string code, string name, Guid facultyId)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        FacultyId = facultyId;
    }

    public void Deactivate() => IsActive = false;
}

public sealed class Bank : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Bin { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private Bank() { }

    public Bank(string code, string name, string bin)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Bin = bin.Trim();
    }

    public void Deactivate() => IsActive = false;
}
