namespace DNTU.SkillBridge.Application.Common;

public sealed record ApiResponse<T>(T Data);

public sealed record PagedResponse<T>(IReadOnlyCollection<T> Data, PageMetadata Meta);

public sealed record PageMetadata(int Page, int PageSize, int TotalItems, int TotalPages)
{
    public static PageMetadata Create(int page, int pageSize, int totalItems) =>
        new(page, pageSize, totalItems, (int)Math.Ceiling(totalItems / (double)pageSize));
}

public class PageQuery
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public int Page { get; init; } = DefaultPage;
    public int PageSize { get; init; } = DefaultPageSize;
}
