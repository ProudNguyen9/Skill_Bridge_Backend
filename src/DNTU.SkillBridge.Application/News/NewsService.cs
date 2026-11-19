using System.Text.Json;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Common;
using DNTU.SkillBridge.Domain.News;

namespace DNTU.SkillBridge.Application.News;

public sealed class NewsService(INewsRepository repository, IUnitOfWork unitOfWork) : INewsService
{
    private static readonly HashSet<string> AdminStatuses = ["DRAFT", "SCHEDULED", "PUBLISHED", "WITHDRAWN", "EXPIRED"];

    public async Task<IReadOnlyCollection<PublicNewsCategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var categories = await repository.ListPublicCategoriesWithCountsAsync(now, cancellationToken);
        return categories
            .Select(item => new PublicNewsCategoryResponse(item.Category.Id, item.Category.Name, item.Category.SortOrder, item.ArticleCount))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<NewsCategoryResponse>> ListAdminCategoriesAsync(CancellationToken cancellationToken) =>
        (await repository.ListCategoriesAsync(cancellationToken)).Select(MapCategory).ToArray();

    public async Task<NewsCategoryWriteResult<NewsCategoryResponse>> CreateCategoryAsync(
        UpsertNewsCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var name = NormalizeCategoryName(request.Name);
        var validation = ValidateCategoryName(name);
        if (validation is not null) return new NewsCategoryWriteResult<NewsCategoryResponse>(NewsCategoryWriteStatus.Invalid, Error: validation);
        if (await repository.CategoryNameExistsAsync(name, null, cancellationToken))
            return new NewsCategoryWriteResult<NewsCategoryResponse>(NewsCategoryWriteStatus.Conflict, Error: "Tên chuyên mục đã được sử dụng.");

        var category = new NewsCategory(name, request.SortOrder);
        repository.AddCategory(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new NewsCategoryWriteResult<NewsCategoryResponse>(NewsCategoryWriteStatus.Success, MapCategory(category));
    }

    public async Task<NewsCategoryWriteResult<NewsCategoryResponse>> UpdateCategoryAsync(
        Guid categoryId,
        UpsertNewsCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await repository.FindCategoryByIdAsync(categoryId, cancellationToken);
        if (category is null) return new NewsCategoryWriteResult<NewsCategoryResponse>(NewsCategoryWriteStatus.NotFound);

        var name = NormalizeCategoryName(request.Name);
        var validation = ValidateCategoryName(name);
        if (validation is not null) return new NewsCategoryWriteResult<NewsCategoryResponse>(NewsCategoryWriteStatus.Invalid, Error: validation);
        if (await repository.CategoryNameExistsAsync(name, categoryId, cancellationToken))
            return new NewsCategoryWriteResult<NewsCategoryResponse>(NewsCategoryWriteStatus.Conflict, Error: "Tên chuyên mục đã được sử dụng.");

        var oldName = category.Name;
        category.Update(name, request.SortOrder);
        if (!string.Equals(oldName, category.Name, StringComparison.Ordinal))
        {
            var articles = await repository.ListArticlesByCategoryAsync(oldName, cancellationToken);
            foreach (var article in articles) article.RenameCategory(category.Name);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new NewsCategoryWriteResult<NewsCategoryResponse>(NewsCategoryWriteStatus.Success, MapCategory(category));
    }

    public async Task<NewsCategoryWriteResult<NewsCategoryResponse>> DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await repository.FindCategoryByIdAsync(categoryId, cancellationToken);
        if (category is null) return new NewsCategoryWriteResult<NewsCategoryResponse>(NewsCategoryWriteStatus.NotFound);
        if (await repository.HasArticlesInCategoryAsync(category.Name, cancellationToken))
            return new NewsCategoryWriteResult<NewsCategoryResponse>(NewsCategoryWriteStatus.InUse, Error: "Chuyên mục vẫn còn bài viết. Hãy chuyển các bài viết sang chuyên mục khác trước khi xóa.");

        repository.RemoveCategory(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new NewsCategoryWriteResult<NewsCategoryResponse>(NewsCategoryWriteStatus.Success, MapCategory(category));
    }

    public async Task<PagedResponse<PublicNewsArticleSummaryResponse>> ListPublicAsync(PublicNewsListQuery query, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var pin = await repository.GetPinSettingsAsync(cancellationToken);
        var pinnedArticleId = pin is not null && (pin.PinnedUntilUtc is null || pin.PinnedUntilUtc > now)
            ? pin.PinnedArticleId
            : null;
        var (items, totalItems) = await repository.ListPublicAsync(
            Normalize(query.Search), Normalize(query.Category), pinnedArticleId, now, query.Page, query.PageSize, cancellationToken);
        var data = items.Select(item => MapPublicSummary(item, item.Id == pinnedArticleId)).ToArray();
        return new PagedResponse<PublicNewsArticleSummaryResponse>(data, PageMetadata.Create(query.Page, query.PageSize, totalItems));
    }

    public async Task<PublicNewsArticleDetailResponse?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var article = await repository.FindPublicBySlugAsync(slug, now, cancellationToken);
        if (article is null) return null;

        var pin = await repository.GetPinSettingsAsync(cancellationToken);
        var isPinned = pin is not null && pin.PinnedArticleId == article.Id && (pin.PinnedUntilUtc is null || pin.PinnedUntilUtc > now);
        return new PublicNewsArticleDetailResponse(
            article.Id, article.Slug, article.Title, article.Category, article.Excerpt, ReadContent(article.ContentJson),
            article.CoverImageUrl, article.CoverImageAlt, article.Source, article.ReadTimeMinutes,
            article.PublishAtUtc!.Value, isPinned);
    }

    public async Task<PagedResponse<AdminNewsArticleResponse>> ListAdminAsync(AdminNewsListQuery query, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var status = Normalize(query.Status)?.ToUpperInvariant();
        var (items, totalItems) = await repository.ListAdminAsync(
            Normalize(query.Search), Normalize(query.Category), status, now, query.Page, query.PageSize, cancellationToken);
        var pin = await repository.GetPinSettingsAsync(cancellationToken);
        var data = items.Select(item =>
        {
            var isPinned = item.Id == pin?.PinnedArticleId && (pin.PinnedUntilUtc is null || pin.PinnedUntilUtc > now);
            return MapAdmin(item, isPinned, isPinned ? pin?.PinnedUntilUtc : null);
        }).ToArray();
        return new PagedResponse<AdminNewsArticleResponse>(data, PageMetadata.Create(query.Page, query.PageSize, totalItems));
    }

    public async Task<AdminNewsArticleResponse?> GetAdminByIdAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var article = await repository.FindByIdAsync(articleId, cancellationToken);
        if (article is null) return null;
        var now = DateTimeOffset.UtcNow;
        var pin = await repository.GetPinSettingsAsync(cancellationToken);
        var isPinned = article.Id == pin?.PinnedArticleId && (pin.PinnedUntilUtc is null || pin.PinnedUntilUtc > now);
        return MapAdmin(article, isPinned, isPinned ? pin?.PinnedUntilUtc : null);
    }

    public async Task<NewsWriteResult<AdminNewsArticleResponse>> CreateAsync(UpsertNewsArticleRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(request, cancellationToken);
        if (validation.Error is not null) return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.Invalid, Error: validation.Error);

        var baseSlug = SlugBuilder.Create(request.Title);
        if (baseSlug.Length == 0) return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.Invalid, Error: "Tiêu đề không tạo được đường dẫn bài viết.");
        var slug = baseSlug;
        for (var suffix = 2; await repository.SlugExistsAsync(slug, cancellationToken); suffix++)
            slug = $"{baseSlug}-{suffix}";

        var article = CreateArticle(slug, request, validation.Category!.Name);
        repository.AddArticle(article);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.Success, MapAdmin(article, false, null));
    }

    public async Task<NewsWriteResult<AdminNewsArticleResponse>> UpdateAsync(Guid articleId, UpsertNewsArticleRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(request, cancellationToken);
        if (validation.Error is not null) return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.Invalid, Error: validation.Error);
        var article = await repository.FindByIdAsync(articleId, cancellationToken);
        if (article is null) return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.NotFound);

        UpdateArticle(article, request, validation.Category!.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ResultAsync(article, cancellationToken);
    }

    public async Task<NewsWriteResult<AdminNewsArticleResponse>> SaveDraftAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var article = await repository.FindByIdAsync(articleId, cancellationToken);
        if (article is null) return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.NotFound);
        article.SaveAsDraft();
        await ClearPinIfArticleAsync(articleId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ResultAsync(article, cancellationToken);
    }

    public async Task<NewsWriteResult<AdminNewsArticleResponse>> PublishAsync(Guid articleId, PublishNewsArticleRequest request, CancellationToken cancellationToken)
    {
        var article = await repository.FindByIdAsync(articleId, cancellationToken);
        if (article is null) return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.NotFound);
        var now = DateTimeOffset.UtcNow;
        var publishAt = request.PublishAtUtc?.ToUniversalTime() ?? now;
        var unpublishAt = request.UnpublishAtUtc?.ToUniversalTime();
        if (unpublishAt is not null && unpublishAt <= publishAt)
            return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.Invalid, Error: "Ngày gỡ phải sau ngày xuất bản.");

        article.Publish(publishAt, unpublishAt);
        if (publishAt > now) await ClearPinIfArticleAsync(articleId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ResultAsync(article, cancellationToken);
    }

    public async Task<NewsWriteResult<AdminNewsArticleResponse>> WithdrawAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var article = await repository.FindByIdAsync(articleId, cancellationToken);
        if (article is null) return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.NotFound);
        article.Withdraw();
        await ClearPinIfArticleAsync(articleId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ResultAsync(article, cancellationToken);
    }

    public async Task<NewsWriteResult<AdminNewsArticleResponse>> PinAsync(Guid articleId, PinNewsArticleRequest request, CancellationToken cancellationToken)
    {
        var article = await repository.FindByIdAsync(articleId, cancellationToken);
        if (article is null) return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.NotFound);
        var now = DateTimeOffset.UtcNow;
        if (!IsPublic(article, now))
            return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.Invalid, Error: "Chỉ bài đang được xuất bản mới có thể ghim.");
        if (request.PinnedUntilUtc is not null && request.PinnedUntilUtc <= now)
            return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.Invalid, Error: "Thời điểm bỏ ghim phải ở tương lai.");

        var settings = await GetOrCreatePinSettingsAsync(cancellationToken);
        settings.Pin(articleId, request.PinnedUntilUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.Success, MapAdmin(article, true, settings.PinnedUntilUtc));
    }

    public async Task<NewsWriteResult<AdminNewsArticleResponse>> UnpinAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var article = await repository.FindByIdAsync(articleId, cancellationToken);
        if (article is null) return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.NotFound);
        var settings = await repository.GetPinSettingsAsync(cancellationToken);
        if (settings?.PinnedArticleId == articleId) settings.Clear();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new NewsWriteResult<AdminNewsArticleResponse>(NewsWriteStatus.Success, MapAdmin(article, false, null));
    }

    public static string EffectiveStatus(NewsArticle article, DateTimeOffset now)
    {
        if (article.Status == NewsArticleStatus.Draft) return "DRAFT";
        if (article.Status == NewsArticleStatus.Withdrawn) return "WITHDRAWN";
        if (article.PublishAtUtc > now) return "SCHEDULED";
        if (article.UnpublishAtUtc is not null && article.UnpublishAtUtc <= now) return "EXPIRED";
        return "PUBLISHED";
    }

    public static bool IsKnownStatus(string? status) => status is null || AdminStatuses.Contains(status);

    private async Task ClearPinIfArticleAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var settings = await repository.GetPinSettingsAsync(cancellationToken);
        if (settings?.PinnedArticleId == articleId) settings.Clear();
    }

    private async Task<NewsPinSettings> GetOrCreatePinSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await repository.GetPinSettingsAsync(cancellationToken);
        if (settings is not null) return settings;
        settings = new NewsPinSettings();
        repository.AddPinSettings(settings);
        return settings;
    }

    private async Task<NewsWriteResult<AdminNewsArticleResponse>> ResultAsync(NewsArticle article, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var pin = await repository.GetPinSettingsAsync(cancellationToken);
        var isPinned = article.Id == pin?.PinnedArticleId && (pin.PinnedUntilUtc is null || pin.PinnedUntilUtc > now);
        return new NewsWriteResult<AdminNewsArticleResponse>(
            NewsWriteStatus.Success,
            MapAdmin(article, isPinned, isPinned ? pin?.PinnedUntilUtc : null));
    }

    private static NewsArticle CreateArticle(string slug, UpsertNewsArticleRequest request, string categoryName) =>
        new(slug, request.Title, categoryName, request.Excerpt, WriteContent(request.Content), request.CoverImageUrl,
            request.CoverImageAlt, request.Source, request.ReadTimeMinutes);

    private static void UpdateArticle(NewsArticle article, UpsertNewsArticleRequest request, string categoryName) =>
        article.UpdateContent(request.Title, categoryName, request.Excerpt, WriteContent(request.Content),
            request.CoverImageUrl, request.CoverImageAlt, request.Source, request.ReadTimeMinutes);

    private async Task<(string? Error, NewsCategory? Category)> ValidateAsync(UpsertNewsArticleRequest request, CancellationToken cancellationToken)
    {
        if (request.Content is null || request.Content.Count == 0 || request.Content.Any(string.IsNullOrWhiteSpace))
            return ("Bài viết cần có ít nhất một đoạn nội dung.", null);
        if (request.CoverImageUrl is { Length: > 1000 }) return ("Đường dẫn ảnh bìa quá dài.", null);
        var category = await repository.FindCategoryByNameAsync(request.Category.Trim(), cancellationToken);
        return category is null ? ("Chuyên mục tin tức không hợp lệ.", null) : (null, category);
    }

    private static NewsCategoryResponse MapCategory(NewsCategory category) => new(category.Id, category.Name, category.SortOrder);

    private static string NormalizeCategoryName(string? name) => string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? ValidateCategoryName(string name)
    {
        if (name.Length is < 2 or > 100) return "Tên chuyên mục phải có từ 2 đến 100 ký tự.";
        if (string.Equals(name, "Tất cả", StringComparison.OrdinalIgnoreCase)) return "Tên “Tất cả” được dành cho bộ lọc mặc định.";
        return null;
    }

    private static bool IsPublic(NewsArticle article, DateTimeOffset now) =>
        article.Status == NewsArticleStatus.Published && article.PublishAtUtc <= now &&
        (article.UnpublishAtUtc is null || article.UnpublishAtUtc > now);

    private static PublicNewsArticleSummaryResponse MapPublicSummary(NewsArticle article, bool isPinned) =>
        new(article.Id, article.Slug, article.Title, article.Category, article.Excerpt, article.CoverImageUrl,
            article.CoverImageAlt, article.Source, article.ReadTimeMinutes, article.PublishAtUtc!.Value, isPinned);

    private static AdminNewsArticleResponse MapAdmin(NewsArticle article, bool isPinned, DateTimeOffset? pinnedUntilUtc) =>
        new(article.Id, article.Slug, article.Title, article.Category, article.Excerpt, ReadContent(article.ContentJson),
            article.CoverImageUrl, article.CoverImageAlt, article.Source, article.ReadTimeMinutes,
            EffectiveStatus(article, DateTimeOffset.UtcNow), article.PublishAtUtc, article.UnpublishAtUtc,
            isPinned, pinnedUntilUtc, article.CreatedAt, article.UpdatedAt);

    private static string WriteContent(IReadOnlyList<string> paragraphs) =>
        JsonSerializer.Serialize(paragraphs.Select(paragraph => paragraph.Trim()).Where(paragraph => paragraph.Length > 0));

    private static IReadOnlyList<string> ReadContent(string value) =>
        JsonSerializer.Deserialize<string[]>(value) ?? [];

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
