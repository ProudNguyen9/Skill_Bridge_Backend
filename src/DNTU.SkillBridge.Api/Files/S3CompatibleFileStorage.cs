using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DNTU.SkillBridge.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Api.Files;

/// <summary>Private S3-compatible storage adapter for MinIO and AWS S3. It only creates scoped, expiring URLs.</summary>
public sealed class S3CompatibleFileStorage(IHttpClientFactory httpClientFactory, IOptions<StorageOptions> options) : IFileStorage
{
    private const string Algorithm = "AWS4-HMAC-SHA256";

    public Task<Uri> CreateUploadUrlAsync(string storageKey, string contentType, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Task.FromResult(CreatePresignedUrl("PUT", storageKey, lifetime, contentType));

    public Task<Uri> CreateDownloadUrlAsync(string storageKey, TimeSpan lifetime, CancellationToken cancellationToken) =>
        Task.FromResult(CreatePresignedUrl("GET", storageKey, lifetime, null));

    public async Task<StoredObjectMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildObjectUri(storageKey));
        SignRequest(request, payloadHash: "UNSIGNED-PAYLOAD");
        using var response = await httpClientFactory.CreateClient(nameof(S3CompatibleFileStorage)).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        // Do not trust provider checksum or Content-Type metadata, which was supplied by the uploader.
        // Files in Task 23 are capped at 50 MiB, so bounded in-memory inspection is intentional here.
        const long maximumInspectableBytes = 50L * 1024 * 1024;
        var declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength is <= 0 or > maximumInspectableBytes) return null;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var content = new MemoryStream(declaredLength is { } length ? checked((int)length) : 0);
        await source.CopyToAsync(content, cancellationToken);
        if (content.Length == 0 || content.Length > maximumInspectableBytes) return null;

        var bytes = content.GetBuffer().AsSpan(0, checked((int)content.Length));
        var signature = DetectSignature(bytes);
        var contentType = DetectContentType(bytes, signature);
        return new StoredObjectMetadata(content.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)), contentType, signature);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, BuildObjectUri(storageKey));
        SignRequest(request, payloadHash: "UNSIGNED-PAYLOAD");
        using var response = await httpClientFactory.CreateClient(nameof(S3CompatibleFileStorage)).SendAsync(request, cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();
    }

    private Uri CreatePresignedUrl(string method, string storageKey, TimeSpan lifetime, string? contentType)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromHours(1)) throw new ArgumentOutOfRangeException(nameof(lifetime));
        var settings = options.Value;
        var now = DateTimeOffset.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var timestamp = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var credentialScope = $"{dateStamp}/{settings.Region}/s3/aws4_request";
        var uri = BuildObjectUri(storageKey);
        var query = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = Algorithm,
            ["X-Amz-Credential"] = $"{settings.AccessKey}/{credentialScope}",
            ["X-Amz-Date"] = timestamp,
            ["X-Amz-Expires"] = ((int)lifetime.TotalSeconds).ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = "host"
        };
        var canonicalQuery = CanonicalQuery(query);
        var canonicalRequest = $"{method}\n{uri.AbsolutePath}\n{canonicalQuery}\nhost:{uri.Host}{PortSuffix(uri)}\n\nhost\nUNSIGNED-PAYLOAD";
        var stringToSign = $"{Algorithm}\n{timestamp}\n{credentialScope}\n{HexSha256(canonicalRequest)}";
        query["X-Amz-Signature"] = Hex(Hmac(SigningKey(dateStamp), stringToSign));
        return new UriBuilder(uri) { Query = CanonicalQuery(query) }.Uri;
    }

    private void SignRequest(HttpRequestMessage request, string payloadHash)
    {
        var settings = options.Value;
        var now = DateTimeOffset.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var timestamp = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var host = request.RequestUri!.Host + PortSuffix(request.RequestUri);
        request.Headers.TryAddWithoutValidation("x-amz-date", timestamp);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        var canonicalHeaders = $"host:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{timestamp}\n";
        var signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest = $"{request.Method.Method}\n{request.RequestUri.AbsolutePath}\n{request.RequestUri.Query.TrimStart('?')}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var scope = $"{dateStamp}/{settings.Region}/s3/aws4_request";
        var signature = Hex(Hmac(SigningKey(dateStamp), $"{Algorithm}\n{timestamp}\n{scope}\n{HexSha256(canonicalRequest)}"));
        request.Headers.TryAddWithoutValidation("Authorization", $"{Algorithm} Credential={settings.AccessKey}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}");
    }

    private Uri BuildObjectUri(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains("..", StringComparison.Ordinal) || storageKey.StartsWith('/')) throw new ArgumentException("Invalid storage key.", nameof(storageKey));
        var settings = options.Value;
        var endpoint = new Uri(settings.Endpoint!, UriKind.Absolute);
        var authority = endpoint.GetLeftPart(UriPartial.Authority);
        var escapedKey = string.Join('/', storageKey.Split('/').Select(Uri.EscapeDataString));
        return settings.UsePathStyle
            ? new Uri($"{authority.TrimEnd('/')}/{Uri.EscapeDataString(settings.Bucket!)}/{escapedKey}")
            : new UriBuilder(endpoint.Scheme, $"{settings.Bucket}.{endpoint.Host}", endpoint.IsDefaultPort ? -1 : endpoint.Port, escapedKey).Uri;
    }

    private byte[] SigningKey(string dateStamp)
    {
        var settings = options.Value;
        var date = Hmac(Encoding.UTF8.GetBytes($"AWS4{settings.SecretKey}"), dateStamp);
        return Hmac(Hmac(Hmac(date, settings.Region), "s3"), "aws4_request");
    }

    private static string PortSuffix(Uri uri) => uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
    private static string CanonicalQuery(IEnumerable<KeyValuePair<string, string>> values) => string.Join("&", values.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
    private static byte[] Hmac(byte[] key, string text) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(text));
    private static string HexSha256(string text) => Hex(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);

    private static FileSignature DetectSignature(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith("%PDF-"u8)) return FileSignature.Pdf;
        if (bytes.Length >= 3 && bytes[..3].SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF })) return FileSignature.Jpeg;
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return FileSignature.Png;
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return FileSignature.Webp;
        if (bytes.Length >= 4 && bytes[..4].SequenceEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 })) return FileSignature.Zip;
        return FileSignature.Unknown;
    }

    private static string DetectContentType(ReadOnlySpan<byte> bytes, FileSignature signature) => signature switch
    {
        FileSignature.Pdf => "application/pdf",
        FileSignature.Jpeg => "image/jpeg",
        FileSignature.Png => "image/png",
        FileSignature.Webp => "image/webp",
        FileSignature.Zip when IsOfficeOpenXml(bytes) => DetectOfficeOpenXmlContentType(bytes),
        FileSignature.Zip => "application/zip",
        _ => "application/octet-stream"
    };

    private static bool IsOfficeOpenXml(ReadOnlySpan<byte> bytes) =>
        bytes.IndexOf("[Content_Types].xml"u8) >= 0 &&
        (bytes.IndexOf("word/"u8) >= 0 || bytes.IndexOf("ppt/"u8) >= 0);

    private static string DetectOfficeOpenXmlContentType(ReadOnlySpan<byte> bytes) =>
        bytes.IndexOf("ppt/"u8) >= 0
            ? "application/vnd.openxmlformats-officedocument.presentationml.presentation"
            : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
}