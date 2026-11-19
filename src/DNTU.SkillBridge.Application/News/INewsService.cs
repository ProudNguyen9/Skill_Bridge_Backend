using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.News;

public interface INewsService
{
    Task<IReadOnlyCollection<PublicNewsCategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<NewsCategoryResponse>> ListAdminCategoriesAsync(CancellationToken cancellationToken);
    Task<NewsCategoryWriteResult<NewsCategoryResponse>> CreateCategoryAsync(UpsertNewsCategoryRequest request, CancellationToken cancellationToken);
    Task<NewsCategoryWriteResult<NewsCategoryResponse>> UpdateCategoryAsync(Guid categoryId, UpsertNewsCategoryRequest request, CancellationToken cancellationToken);
    Task<NewsCategoryWriteResult<NewsCategoryResponse>> DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken);
    Task<PagedResponse<PublicNewsArticleSummaryResponse>> ListPublicAsync(PublicNewsListQuery query, CancellationToken cancellationToken);
    Task<PublicNewsArticleDetailResponse?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<PagedResponse<AdminNewsArticleResponse>> ListAdminAsync(AdminNewsListQuery query, CancellationToken cancellationToken);
    Task<AdminNewsArticleResponse?> GetAdminByIdAsync(Guid articleId, CancellationToken cancellationToken);
    Task<NewsWriteResult<AdminNewsArticleResponse>> CreateAsync(UpsertNewsArticleRequest request, CancellationToken cancellationToken);
    Task<NewsWriteResult<AdminNewsArticleResponse>> UpdateAsync(Guid articleId, UpsertNewsArticleRequest request, CancellationToken cancellationToken);
    Task<NewsWriteResult<AdminNewsArticleResponse>> SaveDraftAsync(Guid articleId, CancellationToken cancellationToken);
    Task<NewsWriteResult<AdminNewsArticleResponse>> PublishAsync(Guid articleId, PublishNewsArticleRequest request, CancellationToken cancellationToken);
    Task<NewsWriteResult<AdminNewsArticleResponse>> WithdrawAsync(Guid articleId, CancellationToken cancellationToken);
    Task<NewsWriteResult<AdminNewsArticleResponse>> PinAsync(Guid articleId, PinNewsArticleRequest request, CancellationToken cancellationToken);
    Task<NewsWriteResult<AdminNewsArticleResponse>> UnpinAsync(Guid articleId, CancellationToken cancellationToken);
}
