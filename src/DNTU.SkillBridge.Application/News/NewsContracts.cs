using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.News;

public sealed class PublicNewsListQuery : PageQuery
{
    [StringLength(200)] public string? Search { get; init; }
    [StringLength(100)] public string? Category { get; init; }
}

public sealed class AdminNewsListQuery : PageQuery
{
    [StringLength(200)] public string? Search { get; init; }
    [StringLength(100)] public string? Category { get; init; }
    [StringLength(20)] public string? Status { get; init; }
}

public sealed class UpsertNewsArticleRequest
{
    [Required, StringLength(300, MinimumLength = 3)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(100)] public string Category { get; init; } = string.Empty;
    [Required, StringLength(600, MinimumLength = 5)] public string Excerpt { get; init; } = string.Empty;
    [Required, MinLength(1), MaxLength(60)] public IReadOnlyList<string> Content { get; init; } = [];
    [StringLength(1000)] public string? CoverImageUrl { get; init; }
    [StringLength(300)] public string? CoverImageAlt { get; init; }
    [StringLength(200)] public string? Source { get; init; }
    [Range(1, 60)] public int ReadTimeMinutes { get; init; } = 3;
}

public sealed class PublishNewsArticleRequest
{
    public DateTimeOffset? PublishAtUtc { get; init; }
    public DateTimeOffset? UnpublishAtUtc { get; init; }
}

public sealed class PinNewsArticleRequest
{
    public DateTimeOffset? PinnedUntilUtc { get; init; }
}

public sealed class UpsertNewsCategoryRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; init; } = string.Empty;
    [Range(0, 9999)] public int SortOrder { get; init; }
}

public sealed record NewsCategoryResponse(Guid Id, string Name, int SortOrder);

public sealed record PublicNewsCategoryResponse(Guid Id, string Name, int SortOrder, int ArticleCount);

public sealed record PublicNewsArticleSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    string Category,
    string Excerpt,
    string? CoverImageUrl,
    string? CoverImageAlt,
    string? Source,
    int ReadTimeMinutes,
    DateTimeOffset PublishedAtUtc,
    bool IsPinned);

public sealed record PublicNewsArticleDetailResponse(
    Guid Id,
    string Slug,
    string Title,
    string Category,
    string Excerpt,
    IReadOnlyList<string> Content,
    string? CoverImageUrl,
    string? CoverImageAlt,
    string? Source,
    int ReadTimeMinutes,
    DateTimeOffset PublishedAtUtc,
    bool IsPinned);

public sealed record AdminNewsArticleResponse(
    Guid Id,
    string Slug,
    string Title,
    string Category,
    string Excerpt,
    IReadOnlyList<string> Content,
    string? CoverImageUrl,
    string? CoverImageAlt,
    string? Source,
    int ReadTimeMinutes,
    string Status,
    DateTimeOffset? PublishAtUtc,
    DateTimeOffset? UnpublishAtUtc,
    bool IsPinned,
    DateTimeOffset? PinnedUntilUtc,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public enum NewsWriteStatus
{
    Success,
    NotFound,
    Conflict,
    Invalid
}

public sealed record NewsWriteResult<T>(NewsWriteStatus Status, T? Value = null, string? Error = null) where T : class;

public enum NewsCategoryWriteStatus
{
    Success,
    NotFound,
    Conflict,
    InUse,
    Invalid
}

public sealed record NewsCategoryWriteResult<T>(NewsCategoryWriteStatus Status, T? Value = null, string? Error = null) where T : class;
