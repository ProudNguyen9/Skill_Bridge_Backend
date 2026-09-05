using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Files;

/// <summary>Metadata and lifecycle state for a server-authorized uploaded object.</summary>
public sealed class FileRecord : AuditableEntity
{
    public Guid UploadedByUserId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long ExpectedSizeBytes { get; private set; }
    public string? ChecksumSha256 { get; private set; }
    public FileUploadStatus Status { get; private set; } = FileUploadStatus.PENDING;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private FileRecord() { }

    public FileRecord(Guid uploadedByUserId, Guid? projectId, string originalFileName, string storageKey, string contentType, long expectedSizeBytes, DateTimeOffset expiresAt)
    {
        UploadedByUserId = uploadedByUserId;
        ProjectId = projectId;
        OriginalFileName = string.IsNullOrWhiteSpace(originalFileName) ? throw new ArgumentException("A file name is required.", nameof(originalFileName)) : originalFileName.Trim();
        StorageKey = storageKey;
        ContentType = contentType;
        ExpectedSizeBytes = expectedSizeBytes;
        ExpiresAt = expiresAt;
    }

    public void Complete(string checksumSha256, DateTimeOffset now)
    {
        if (Status != FileUploadStatus.PENDING || ExpiresAt <= now) throw new InvalidOperationException("The upload request is no longer valid.");
        Status = FileUploadStatus.COMPLETED;
        ChecksumSha256 = checksumSha256;
        CompletedAt = now;
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status == FileUploadStatus.PENDING && ExpiresAt <= now) Status = FileUploadStatus.EXPIRED;
    }

    public void Delete() => Status = FileUploadStatus.DELETED;
}
