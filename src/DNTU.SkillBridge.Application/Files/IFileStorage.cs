namespace DNTU.SkillBridge.Application.Files;

public sealed record StoredObjectMetadata(long Length, string ChecksumSha256, string DetectedContentType, FileSignature Signature);

public enum FileSignature
{
    Unknown = 0,
    Pdf = 1,
    Jpeg = 2,
    Png = 3,
    Webp = 4,
    Zip = 5,
    OfficeOpenXml = 6
}

/// <summary>Storage-provider boundary; production adapters may issue provider-specific signed URLs.</summary>
public interface IFileStorage
{
    Task<Uri> CreateUploadUrlAsync(string storageKey, string contentType, TimeSpan lifetime, CancellationToken cancellationToken);
    Task<StoredObjectMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken);
    Task<Uri> CreateDownloadUrlAsync(string storageKey, TimeSpan lifetime, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

/// <summary>Safe development placeholder. It deliberately does not expose credentials or make a bucket public.</summary>
public sealed class DisabledFileStorage : IFileStorage
{
    private static InvalidOperationException Disabled() => new("File storage is disabled. Configure Storage:Enabled and a production IFileStorage provider.");

    public Task<Uri> CreateUploadUrlAsync(string storageKey, string contentType, TimeSpan lifetime, CancellationToken cancellationToken) => Task.FromException<Uri>(Disabled());
    public Task<StoredObjectMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken) => Task.FromException<StoredObjectMetadata?>(Disabled());
    public Task<Uri> CreateDownloadUrlAsync(string storageKey, TimeSpan lifetime, CancellationToken cancellationToken) => Task.FromException<Uri>(Disabled());
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.FromException(Disabled());
}
