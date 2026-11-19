using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.News;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/admin/news")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.SuperAdmin)]
[Produces("application/json")]
public sealed class AdminNewsController(INewsService newsService) : ControllerBase
{
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<NewsCategoryResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<NewsCategoryResponse>>>> ListCategories(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyCollection<NewsCategoryResponse>>(await newsService.ListAdminCategoriesAsync(cancellationToken)));

    [HttpPost("categories")]
    [ProducesResponseType(typeof(ApiResponse<NewsCategoryResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<NewsCategoryResponse>>> CreateCategory(
        [FromBody] UpsertNewsCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await newsService.CreateCategoryAsync(request, cancellationToken);
        return result.Status == NewsCategoryWriteStatus.Success
            ? Created($"/api/v1/admin/news/categories/{result.Value!.Id}", new ApiResponse<NewsCategoryResponse>(result.Value))
            : RespondCategory(result);
    }

    [HttpPut("categories/{categoryId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<NewsCategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<NewsCategoryResponse>>> UpdateCategory(
        Guid categoryId,
        [FromBody] UpsertNewsCategoryRequest request,
        CancellationToken cancellationToken) =>
        RespondCategory(await newsService.UpdateCategoryAsync(categoryId, request, cancellationToken));

    [HttpDelete("categories/{categoryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(Guid categoryId, CancellationToken cancellationToken)
    {
        var result = await newsService.DeleteCategoryAsync(categoryId, cancellationToken);
        if (result.Status == NewsCategoryWriteStatus.Success) return NoContent();
        return result.Status switch
        {
            NewsCategoryWriteStatus.NotFound => NotFound(),
            NewsCategoryWriteStatus.InUse => Conflict(new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AdminNewsArticleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AdminNewsArticleResponse>>> List(
        [FromQuery] AdminNewsListQuery query, CancellationToken cancellationToken)
    {
        if (!ValidPage(query.Page, query.PageSize)) return BadRequest("Page or PageSize is outside the allowed range.");
        if (!NewsService.IsKnownStatus(query.Status?.Trim().ToUpperInvariant())) return BadRequest("Status is not supported.");
        return Ok(await newsService.ListAdminAsync(query, cancellationToken));
    }

    [HttpGet("{articleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminNewsArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminNewsArticleResponse>>> GetAdminById(
        Guid articleId, CancellationToken cancellationToken)
    {
        var article = await newsService.GetAdminByIdAsync(articleId, cancellationToken);
        return article is null ? NotFound() : Ok(new ApiResponse<AdminNewsArticleResponse>(article));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminNewsArticleResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AdminNewsArticleResponse>>> Create(
        [FromBody] UpsertNewsArticleRequest request, CancellationToken cancellationToken)
    {
        var result = await newsService.CreateAsync(request, cancellationToken);
        if (result.Status == NewsWriteStatus.Invalid) return BadRequest(new { message = result.Error });
        return Created($"/api/v1/admin/news/{result.Value!.Id}", new ApiResponse<AdminNewsArticleResponse>(result.Value));
    }

    [HttpPut("{articleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminNewsArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminNewsArticleResponse>>> Update(
        Guid articleId, [FromBody] UpsertNewsArticleRequest request, CancellationToken cancellationToken)
    {
        var result = await newsService.UpdateAsync(articleId, request, cancellationToken);
        return Respond(result);
    }

    [HttpPost("{articleId:guid}/draft")]
    [ProducesResponseType(typeof(ApiResponse<AdminNewsArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminNewsArticleResponse>>> SaveDraft(Guid articleId, CancellationToken cancellationToken) =>
        Respond(await newsService.SaveDraftAsync(articleId, cancellationToken));

    [HttpPost("{articleId:guid}/publish")]
    [ProducesResponseType(typeof(ApiResponse<AdminNewsArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminNewsArticleResponse>>> Publish(
        Guid articleId, [FromBody] PublishNewsArticleRequest request, CancellationToken cancellationToken) =>
        Respond(await newsService.PublishAsync(articleId, request, cancellationToken));

    [HttpPost("{articleId:guid}/unpublish")]
    [ProducesResponseType(typeof(ApiResponse<AdminNewsArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminNewsArticleResponse>>> Unpublish(Guid articleId, CancellationToken cancellationToken) =>
        Respond(await newsService.WithdrawAsync(articleId, cancellationToken));

    [HttpPost("{articleId:guid}/pin")]
    [ProducesResponseType(typeof(ApiResponse<AdminNewsArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminNewsArticleResponse>>> Pin(
        Guid articleId, [FromBody] PinNewsArticleRequest request, CancellationToken cancellationToken) =>
        Respond(await newsService.PinAsync(articleId, request, cancellationToken));

    [HttpDelete("{articleId:guid}/pin")]
    [ProducesResponseType(typeof(ApiResponse<AdminNewsArticleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminNewsArticleResponse>>> Unpin(Guid articleId, CancellationToken cancellationToken) =>
        Respond(await newsService.UnpinAsync(articleId, cancellationToken));

    private ActionResult<ApiResponse<AdminNewsArticleResponse>> Respond(NewsWriteResult<AdminNewsArticleResponse> result) => result.Status switch
    {
        NewsWriteStatus.NotFound => NotFound(),
        NewsWriteStatus.Conflict => Conflict(new { message = result.Error }),
        NewsWriteStatus.Invalid => BadRequest(new { message = result.Error }),
        _ => Ok(new ApiResponse<AdminNewsArticleResponse>(result.Value!))
    };

    private ActionResult<ApiResponse<NewsCategoryResponse>> RespondCategory(NewsCategoryWriteResult<NewsCategoryResponse> result) => result.Status switch
    {
        NewsCategoryWriteStatus.NotFound => NotFound(),
        NewsCategoryWriteStatus.Conflict or NewsCategoryWriteStatus.InUse => Conflict(new { message = result.Error }),
        NewsCategoryWriteStatus.Invalid => BadRequest(new { message = result.Error }),
        _ => Ok(new ApiResponse<NewsCategoryResponse>(result.Value!))
    };

    private static bool ValidPage(int page, int pageSize) =>
        page > 0 && pageSize is > 0 and <= PageQuery.MaximumPageSize;
}
