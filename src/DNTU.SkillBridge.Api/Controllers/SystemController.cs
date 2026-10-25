using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("health")]
    [ProducesResponseType(typeof(ApiResponse<SystemHealthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SystemHealthResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<SystemHealthResponse>>> GetHealth(
        [FromServices] HealthCheckService healthCheckService,
        CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);
        var response = new ApiResponse<SystemHealthResponse>(new SystemHealthResponse(
            report.Status.ToString().ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            report.Entries.Select(entry => new SystemHealthCheckResponse(
                entry.Key,
                entry.Value.Status.ToString().ToLowerInvariant(),
                entry.Value.Duration.TotalMilliseconds)).ToArray()));

        if (report.Status == HealthStatus.Unhealthy)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        return Ok(response);
    }

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
public sealed record SystemHealthResponse(
    string Status,
    DateTimeOffset CheckedAt,
    IReadOnlyList<SystemHealthCheckResponse> Checks);
public sealed record SystemHealthCheckResponse(string Name, string Status, double DurationMs);
