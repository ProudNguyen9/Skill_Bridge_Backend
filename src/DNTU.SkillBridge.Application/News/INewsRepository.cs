using DNTU.SkillBridge.Domain.News;

namespace DNTU.SkillBridge.Application.News;

public interface INewsRepository
{
    Task<IReadOnlyCollection<NewsCategory>> ListCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<(NewsCategory Category, int ArticleCount)>> ListPublicCategoriesWithCountsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<NewsCategory?> FindCategoryByIdAsync(Guid categoryId, CancellationToken cancellationToken);
    Task<NewsCategory?> FindCategoryByNameAsync(string name, CancellationToken cancellationToken);
    Task<bool> CategoryNameExistsAsync(string name, Guid? excludingCategoryId, CancellationToken cancellationToken);
    Task<bool> HasArticlesInCategoryAsync(string categoryName, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<NewsArticle>> ListArticlesByCategoryAsync(string categoryName, CancellationToken cancellationToken);
    void AddCategory(NewsCategory category);
    void RemoveCategory(NewsCategory category);

    Task<(IReadOnlyCollection<NewsArticle> Items, int TotalItems)> ListAdminAsync(
        string? search,
        string? category,
        string? status,
        DateTimeOffset now,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<(IReadOnlyCollection<NewsArticle> Items, int TotalItems)> ListPublicAsync(
        string? search,
        string? category,
        Guid? pinnedArticleId,
        DateTimeOffset now,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<NewsArticle?> FindByIdAsync(Guid articleId, CancellationToken cancellationToken);
    Task<NewsArticle?> FindPublicBySlugAsync(string slug, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);
    Task<NewsPinSettings?> GetPinSettingsAsync(CancellationToken cancellationToken);
    void AddArticle(NewsArticle article);
    void AddPinSettings(NewsPinSettings settings);
}
