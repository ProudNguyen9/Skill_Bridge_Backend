using DNTU.SkillBridge.Application.News;
using DNTU.SkillBridge.Domain.News;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

public sealed class NewsRepository(AppDbContext dbContext) : INewsRepository
{
    public async Task<IReadOnlyCollection<NewsCategory>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        await dbContext.NewsCategories.AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<(NewsCategory Category, int ArticleCount)>> ListPublicCategoriesWithCountsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var publicArticles = dbContext.NewsArticles.AsNoTracking()
            .Where(article => article.Status == NewsArticleStatus.Published && article.PublishAtUtc <= now &&
                (article.UnpublishAtUtc == null || article.UnpublishAtUtc > now));
        var categories = await dbContext.NewsCategories.AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new
            {
                Category = category,
                ArticleCount = publicArticles.Count(article => article.Category == category.Name)
            })
            .ToArrayAsync(cancellationToken);

        return categories.Select(item => (item.Category, item.ArticleCount)).ToArray();
    }

    public Task<NewsCategory?> FindCategoryByIdAsync(Guid categoryId, CancellationToken cancellationToken) =>
        dbContext.NewsCategories.FirstOrDefaultAsync(category => category.Id == categoryId, cancellationToken);

    public Task<NewsCategory?> FindCategoryByNameAsync(string name, CancellationToken cancellationToken) =>
        dbContext.NewsCategories.FirstOrDefaultAsync(category => category.Name == name, cancellationToken);

    public Task<bool> CategoryNameExistsAsync(string name, Guid? excludingCategoryId, CancellationToken cancellationToken) =>
        dbContext.NewsCategories.AnyAsync(category => category.Name == name &&
            (excludingCategoryId == null || category.Id != excludingCategoryId), cancellationToken);

    public Task<bool> HasArticlesInCategoryAsync(string categoryName, CancellationToken cancellationToken) =>
        dbContext.NewsArticles.AnyAsync(article => article.Category == categoryName, cancellationToken);

    public async Task<IReadOnlyCollection<NewsArticle>> ListArticlesByCategoryAsync(string categoryName, CancellationToken cancellationToken) =>
        await dbContext.NewsArticles.Where(article => article.Category == categoryName).ToArrayAsync(cancellationToken);

    public void AddCategory(NewsCategory category) => dbContext.NewsCategories.Add(category);

    public void RemoveCategory(NewsCategory category) => dbContext.NewsCategories.Remove(category);

    public async Task<(IReadOnlyCollection<NewsArticle> Items, int TotalItems)> ListAdminAsync(
        string? search,
        string? category,
        string? status,
        DateTimeOffset now,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.NewsArticles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(article => article.Title.Contains(term) || article.Excerpt.Contains(term) ||
                (article.Source != null && article.Source.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(article => article.Category == category);

        query = status?.ToUpperInvariant() switch
        {
            "DRAFT" => query.Where(article => article.Status == NewsArticleStatus.Draft),
            "SCHEDULED" => query.Where(article => article.Status == NewsArticleStatus.Published && article.PublishAtUtc > now),
            "PUBLISHED" => query.Where(article => article.Status == NewsArticleStatus.Published && article.PublishAtUtc <= now &&
                (article.UnpublishAtUtc == null || article.UnpublishAtUtc > now)),
            "WITHDRAWN" => query.Where(article => article.Status == NewsArticleStatus.Withdrawn),
            "EXPIRED" => query.Where(article => article.Status == NewsArticleStatus.Published && article.UnpublishAtUtc <= now),
            _ => query
        };

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(article => article.PublishAtUtc)
            .ThenByDescending(article => article.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return (items, totalItems);
    }

    public async Task<(IReadOnlyCollection<NewsArticle> Items, int TotalItems)> ListPublicAsync(
        string? search,
        string? category,
        Guid? pinnedArticleId,
        DateTimeOffset now,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.NewsArticles.AsNoTracking()
            .Where(article => article.Status == NewsArticleStatus.Published && article.PublishAtUtc <= now &&
                (article.UnpublishAtUtc == null || article.UnpublishAtUtc > now));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(article => article.Title.Contains(term) || article.Excerpt.Contains(term) ||
                (article.Source != null && article.Source.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(article => article.Category == category);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(article => article.Id == pinnedArticleId)
            .ThenByDescending(article => article.PublishAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return (items, totalItems);
    }

    public Task<NewsArticle?> FindByIdAsync(Guid articleId, CancellationToken cancellationToken) =>
        dbContext.NewsArticles.FirstOrDefaultAsync(article => article.Id == articleId, cancellationToken);

    public Task<NewsArticle?> FindPublicBySlugAsync(string slug, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.NewsArticles.AsNoTracking().FirstOrDefaultAsync(article => article.Slug == slug &&
            article.Status == NewsArticleStatus.Published && article.PublishAtUtc <= now &&
            (article.UnpublishAtUtc == null || article.UnpublishAtUtc > now), cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        dbContext.NewsArticles.AnyAsync(article => article.Slug == slug, cancellationToken);

    public Task<NewsPinSettings?> GetPinSettingsAsync(CancellationToken cancellationToken) =>
        dbContext.NewsPinSettings.FirstOrDefaultAsync(settings => settings.Id == NewsPinSettings.SingletonId, cancellationToken);

    public void AddArticle(NewsArticle article) => dbContext.NewsArticles.Add(article);

    public void AddPinSettings(NewsPinSettings settings) => dbContext.NewsPinSettings.Add(settings);
}
