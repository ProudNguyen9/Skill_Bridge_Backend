using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.News;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/news")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class NewsController(INewsService newsService) : ControllerBase
{
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<PublicNewsCategoryResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PublicNewsCategoryResponse>>>> ListCategories(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyCollection<PublicNewsCategoryResponse>>(await newsService.ListCategoriesAsync(cancellationToken)));

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PublicNewsArticleSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PublicNewsArticleSummaryResponse>>> List(
        [FromQuery] PublicNewsListQuery query, CancellationToken cancellationToken)
    {
        if (!ValidPage(query.Page, query.PageSize)) return BadRequest("Page or PageSize is outside the allowed range.");
        return Ok(await newsService.ListPublicAsync(query, cancellationToken));
    }

    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ApiResponse<PublicNewsArticleDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PublicNewsArticleDetailResponse>>> GetBySlug(
        string slug, CancellationToken cancellationToken)
    {
        var article = await newsService.GetPublicBySlugAsync(slug, cancellationToken);
        return article is null ? NotFound() : Ok(new ApiResponse<PublicNewsArticleDetailResponse>(article));
    }

    private static bool ValidPage(int page, int pageSize) =>
        page > 0 && pageSize is > 0 and <= PageQuery.MaximumPageSize;
}
