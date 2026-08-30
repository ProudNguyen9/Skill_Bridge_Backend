using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Files;

namespace DNTU.SkillBridge.Application.Files;

public sealed class CreateFileUploadRequest
{
    [Required, StringLength(255, MinimumLength = 1)] public string FileName { get; init; } = string.Empty;
    [Required, StringLength(128)] public string ContentType { get; init; } = string.Empty;
    [Range(1, 50 * 1024 * 1024)] public long SizeBytes { get; init; }
    [Required, RegularExpression("^(cv|image|submission)$", ErrorMessage = "Category must be cv, image, or submission.")] public string Category { get; init; } = string.Empty;
    public Guid? ProjectId { get; init; }
}

public sealed class CompleteFileUploadRequest
{
    [Required, RegularExpression("^[A-Fa-f0-9]{64}$", ErrorMessage = "ChecksumSha256 must be a 64-character hexadecimal SHA-256 digest.")] public string ChecksumSha256 { get; init; } = string.Empty;
}

public sealed record FileRecordResponse(Guid Id, Guid? ProjectId, string FileName, string ContentType, long ExpectedSizeBytes, FileUploadStatus Status, DateTimeOffset ExpiresAt, DateTimeOffset? CompletedAt);
public sealed record FileUploadRequestResponse(FileRecordResponse File, Uri UploadUrl);
