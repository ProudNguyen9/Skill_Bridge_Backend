using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.News;

public enum NewsArticleStatus
{
    Draft,
    Published,
    Withdrawn
}

public sealed class NewsArticle : AuditableEntity
{
    public string Slug { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Excerpt { get; private set; } = string.Empty;
    public string ContentJson { get; private set; } = "[]";
    public string? CoverImageUrl { get; private set; }
    public string? CoverImageAlt { get; private set; }
    public string? Source { get; private set; }
    public int ReadTimeMinutes { get; private set; } = 1;
    public NewsArticleStatus Status { get; private set; } = NewsArticleStatus.Draft;
    public DateTimeOffset? PublishAtUtc { get; private set; }
    public DateTimeOffset? UnpublishAtUtc { get; private set; }

    private NewsArticle() { }

    public NewsArticle(
        string slug,
        string title,
        string category,
        string excerpt,
        string contentJson,
        string? coverImageUrl,
        string? coverImageAlt,
        string? source,
        int readTimeMinutes)
    {
        Slug = slug;
        UpdateContent(title, category, excerpt, contentJson, coverImageUrl, coverImageAlt, source, readTimeMinutes);
    }

    public void UpdateContent(
        string title,
        string category,
        string excerpt,
        string contentJson,
        string? coverImageUrl,
        string? coverImageAlt,
        string? source,
        int readTimeMinutes)
    {
        Title = title.Trim();
        Category = category.Trim();
        Excerpt = excerpt.Trim();
        ContentJson = contentJson;
        CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl.Trim();
        CoverImageAlt = string.IsNullOrWhiteSpace(coverImageAlt) ? null : coverImageAlt.Trim();
        Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
        ReadTimeMinutes = Math.Clamp(readTimeMinutes, 1, 60);
    }

    public void RenameCategory(string categoryName)
    {
        Category = categoryName.Trim();
    }

    public void SaveAsDraft()
    {
        Status = NewsArticleStatus.Draft;
        PublishAtUtc = null;
        UnpublishAtUtc = null;
    }

    public void Publish(DateTimeOffset publishAtUtc, DateTimeOffset? unpublishAtUtc)
    {
        Status = NewsArticleStatus.Published;
        PublishAtUtc = publishAtUtc.ToUniversalTime();
        UnpublishAtUtc = unpublishAtUtc?.ToUniversalTime();
    }

    public void Withdraw()
    {
        Status = NewsArticleStatus.Withdrawn;
        UnpublishAtUtc = null;
    }
}

public sealed class NewsPinSettings : AuditableEntity
{
    public static readonly Guid SingletonId = Guid.Parse("e37e70a5-dcd2-4c93-8e8a-0f7f81200a01");

    public Guid? PinnedArticleId { get; private set; }
    public DateTimeOffset? PinnedUntilUtc { get; private set; }

    public NewsPinSettings()
    {
        Id = SingletonId;
    }

    public void Pin(Guid articleId, DateTimeOffset? pinnedUntilUtc)
    {
        PinnedArticleId = articleId;
        PinnedUntilUtc = pinnedUntilUtc?.ToUniversalTime();
    }

    public void Clear()
    {
        PinnedArticleId = null;
        PinnedUntilUtc = null;
    }
}
