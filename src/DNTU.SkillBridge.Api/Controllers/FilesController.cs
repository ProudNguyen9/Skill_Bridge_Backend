using System.Text.RegularExpressions;
using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Application.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[Tags("Files")]
[Produces("application/json")]
public sealed class FilesController(
    IFileStorage fileStorage,
    IFileService fileService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách files từ S3/Filebase bucket hoặc thông tin chi tiết một file nếu truyền key.
    /// </summary>
    [HttpGet("/files")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<StorageItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFiles(
        [FromQuery] string? prefix = null,
        [FromQuery] string? key = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                var meta = await fileStorage.GetMetadataAsync(key, cancellationToken);
                var downloadUrl = await fileStorage.CreateDownloadUrlAsync(key, TimeSpan.FromHours(1), cancellationToken);
                return Ok(new
                {
                    key,
                    url = downloadUrl.ToString(),
                    metadata = meta
                });
            }

            var items = await fileStorage.ListObjectsAsync(prefix, cancellationToken);
            return Ok(items);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = $"Lỗi khi truy xuất files từ Filebase: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Upload file trực tiếp lên Filebase S3 bucket.
    /// </summary>
    [HttpPost("/files")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFile(
        IFormFile? file,
        [FromForm] string? customKey = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn một file hợp lệ để tải lên." });
        }

        var extension = Path.GetExtension(file.FileName);
        var baseName = Path.GetFileNameWithoutExtension(file.FileName);
        var safeName = Regex.Replace(baseName, @"[^a-zA-Z0-9_\-\.]", "_");
        var storageKey = string.IsNullOrWhiteSpace(customKey)
            ? $"uploads/{DateTime.UtcNow:yyyyMMdd_HHmmss}_{safeName}{extension}"
            : customKey.TrimStart('/');

        try
        {
            await using var stream = file.OpenReadStream();
            var uri = await fileStorage.DirectUploadAsync(storageKey, stream, file.ContentType, cancellationToken);
            var downloadUrl = await fileStorage.CreateDownloadUrlAsync(storageKey, TimeSpan.FromHours(24), cancellationToken);

            return Ok(new
            {
                success = true,
                message = "File tải lên thành công!",
                key = storageKey,
                fileName = file.FileName,
                size = file.Length,
                contentType = file.ContentType,
                directUrl = uri.ToString(),
                downloadUrl = downloadUrl.ToString()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = $"Lỗi khi tải file lên Filebase S3: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Xóa file khỏi S3/Filebase bucket theo key.
    /// </summary>
    [HttpDelete("/files")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteFile(
        [FromQuery] string key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(new { message = "Vui lòng cung cấp tham số 'key' của file cần xóa." });
        }

        try
        {
            await fileStorage.DeleteAsync(key, cancellationToken);
            return Ok(new
            {
                success = true,
                message = $"File '{key}' đã được xóa thành công.",
                key
            });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                message = "Filebase từ chối quyền xóa file (Access Denied). Vui lòng kiểm tra quyền Delete của Access Token trên dashboard Filebase hoặc xóa trực tiếp trên web console Filebase.",
                key
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = $"Lỗi khi xóa file khỏi Filebase S3: {ex.Message}",
                key
            });
        }
    }

    // =========================================================================
    // Legacy internal endpoints (authenticated)
    // =========================================================================

    [Tags("Files (Legacy)")]
    [Authorize]
    [HttpPost("api/v1/files/upload-requests")]
    [ProducesResponseType(typeof(ApiResponse<FileUploadRequestResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<FileUploadRequestResponse>>> CreateUploadRequest(
        CreateFileUploadRequest request,
        CancellationToken cancellationToken)
    {
        var upload = await fileService.CreateUploadRequestAsync(UserId, request, cancellationToken);
        return upload is null
            ? ValidationProblem("File storage, access scope, file type, or size is invalid.")
            : Created(string.Empty, new ApiResponse<FileUploadRequestResponse>(upload));
    }

    [Tags("Files (Legacy)")]
    [Authorize]
    [HttpPost("api/v1/files/{fileId:guid}/complete")]
    [ProducesResponseType(typeof(ApiResponse<FileRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<FileRecordResponse>>> Complete(
        Guid fileId,
        CompleteFileUploadRequest request,
        CancellationToken cancellationToken)
    {
        var file = await fileService.CompleteAsync(UserId, fileId, request, cancellationToken);
        return file is null ? Conflict() : Ok(new ApiResponse<FileRecordResponse>(file));
    }

    [Tags("Files (Legacy)")]
    [Authorize]
    [HttpGet("api/v1/files/{fileId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FileRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FileRecordResponse>>> Get(
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var file = await fileService.GetAsync(UserId, fileId, cancellationToken);
        return file is null ? NotFound() : Ok(new ApiResponse<FileRecordResponse>(file));
    }

    [Tags("Files (Legacy)")]
    [Authorize]
    [HttpGet("api/v1/files/{fileId:guid}/download-url")]
    [ProducesResponseType(typeof(ApiResponse<Uri>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<Uri>>> DownloadUrl(
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var url = await fileService.DownloadAsync(UserId, fileId, cancellationToken);
        return url is null ? NotFound() : Ok(new ApiResponse<Uri>(url));
    }

    [Tags("Files (Legacy)")]
    [Authorize]
    [HttpDelete("api/v1/files/{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid fileId,
        CancellationToken cancellationToken) =>
        await fileService.DeleteAsync(UserId, fileId, cancellationToken) ? NoContent() : NotFound();

    private Guid UserId => currentUser.UserId!.Value;
}
