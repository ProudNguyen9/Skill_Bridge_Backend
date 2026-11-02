using System.Security.Cryptography;
using DNTU.SkillBridge.Application.Files;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace DNTU.SkillBridge.Infrastructure.Files;

public sealed class LocalStorageFileStorage : IFileStorage
{
    private readonly string _webRoot;
    private readonly string _baseUrl;

    public LocalStorageFileStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _webRoot = environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(_webRoot, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var publicBaseUrl = configuration.GetValue<string>("App:PublicBaseUrl")?.TrimEnd('/')
            ?? "http://localhost:5017";
        _baseUrl = publicBaseUrl;
    }

    private static string NormalizeKey(string storageKey)
    {
        var clean = storageKey.TrimStart('/').Replace('\\', '/');
        return clean.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)
            ? clean
            : $"uploads/{clean}";
    }

    public async Task<Uri> DirectUploadAsync(string storageKey, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var key = NormalizeKey(storageKey);
        var fullPath = Path.Combine(_webRoot, key);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, cancellationToken);

        return new Uri($"{_baseUrl}/{key}");
    }

    public Task<Uri> CreateDownloadUrlAsync(string storageKey, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var key = NormalizeKey(storageKey);
        return Task.FromResult(new Uri($"{_baseUrl}/{key}"));
    }

    public Task<Uri> CreateUploadUrlAsync(string storageKey, string contentType, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var key = NormalizeKey(storageKey);
        return Task.FromResult(new Uri($"{_baseUrl}/{key}"));
    }

    public async Task<StoredObjectMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken)
    {
        var key = NormalizeKey(storageKey);
        var fullPath = Path.Combine(_webRoot, key);
        if (!File.Exists(fullPath)) return null;

        var fileInfo = new FileInfo(fullPath);
        await using var stream = File.OpenRead(fullPath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        var checksum = Convert.ToHexString(hash).ToLowerInvariant();

        return new StoredObjectMetadata(fileInfo.Length, checksum, "application/octet-stream", FileSignature.Unknown);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var key = NormalizeKey(storageKey);
        var fullPath = Path.Combine(_webRoot, key);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StorageItemDto>> ListObjectsAsync(string? prefix, CancellationToken cancellationToken)
    {
        var uploadsDir = Path.Combine(_webRoot, "uploads");
        if (!Directory.Exists(uploadsDir)) return Task.FromResult<IReadOnlyList<StorageItemDto>>([]);

        var allFiles = Directory.GetFiles(uploadsDir, "*", SearchOption.AllDirectories);
        var list = new List<StorageItemDto>();
        foreach (var file in allFiles)
        {
            var relPath = Path.GetRelativePath(_webRoot, file).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(prefix) || relPath.StartsWith(prefix.TrimStart('/'), StringComparison.OrdinalIgnoreCase))
            {
                var fi = new FileInfo(file);
                list.Add(new StorageItemDto(relPath, fi.Length, fi.LastWriteTimeUtc, $"{_baseUrl}/{relPath}"));
            }
        }
        return Task.FromResult<IReadOnlyList<StorageItemDto>>(list);
    }
}
