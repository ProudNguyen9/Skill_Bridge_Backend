using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.News;

public sealed class NewsCategory : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    private NewsCategory() { }

    public NewsCategory(string name, int sortOrder)
    {
        Update(name, sortOrder);
    }

    public void Update(string name, int sortOrder)
    {
        Name = name.Trim();
        SortOrder = Math.Max(0, sortOrder);
    }
}
