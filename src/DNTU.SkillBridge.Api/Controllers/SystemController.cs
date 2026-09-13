using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("contract")]
    [ProducesResponseType(typeof(ApiResponse<SystemContractResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<SystemContractResponse>> GetContract() =>
        Ok(new ApiResponse<SystemContractResponse>(new SystemContractResponse("v1", DateTimeOffset.UtcNow)));

    [HttpGet("page-probe")]
    [ProducesResponseType(typeof(PagedResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<PagedResponse<string>> GetPageProbe([FromQuery] PageQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > PageQuery.MaximumPageSize)
        {
            ModelState.AddModelError(nameof(query.PageSize), $"Page must be at least 1 and pageSize must be between 1 and {PageQuery.MaximumPageSize}.");
            return ValidationProblem(ModelState);
        }

        var values = new[] { "first", "second", "third" };
        var page = values.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray();
        return Ok(new PagedResponse<string>(page, PageMetadata.Create(query.Page, query.PageSize, values.Length)));
    }
}

public sealed record SystemContractResponse(string ApiVersion, DateTimeOffset GeneratedAt);
