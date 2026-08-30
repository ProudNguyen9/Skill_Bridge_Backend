using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Api.Files;
using DNTU.SkillBridge.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/files")]
[Authorize]
[Produces("application/json")]
public sealed class FilesController(FileService fileService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("upload-requests")]
    [ProducesResponseType(typeof(ApiResponse<FileUploadRequestResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<FileUploadRequestResponse>>> CreateUploadRequest(CreateFileUploadRequest request, CancellationToken cancellationToken)
    {
        var upload = await fileService.CreateUploadRequestAsync(UserId, request, cancellationToken);
        return upload is null ? ValidationProblem("File storage, access scope, file type, or size is invalid.") : Created(string.Empty, new ApiResponse<FileUploadRequestResponse>(upload));
    }

    [HttpPost("{fileId:guid}/complete")]
    [ProducesResponseType(typeof(ApiResponse<FileRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<FileRecordResponse>>> Complete(Guid fileId, CompleteFileUploadRequest request, CancellationToken cancellationToken)
    {
        var file = await fileService.CompleteAsync(UserId, fileId, request, cancellationToken);
        return file is null ? Conflict() : Ok(new ApiResponse<FileRecordResponse>(file));
    }

    [HttpGet("{fileId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FileRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FileRecordResponse>>> Get(Guid fileId, CancellationToken cancellationToken)
    {
        var file = await fileService.GetAsync(UserId, fileId, cancellationToken);
        return file is null ? NotFound() : Ok(new ApiResponse<FileRecordResponse>(file));
    }

    [HttpGet("{fileId:guid}/download-url")]
    [ProducesResponseType(typeof(ApiResponse<Uri>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<Uri>>> DownloadUrl(Guid fileId, CancellationToken cancellationToken)
    {
        var url = await fileService.DownloadAsync(UserId, fileId, cancellationToken);
        return url is null ? NotFound() : Ok(new ApiResponse<Uri>(url));
    }

    [HttpDelete("{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid fileId, CancellationToken cancellationToken) =>
        await fileService.DeleteAsync(UserId, fileId, cancellationToken) ? NoContent() : NotFound();

    private Guid UserId => currentUser.UserId!.Value;
}
