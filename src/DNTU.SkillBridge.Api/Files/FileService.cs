using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Domain.Files;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Api.Files;

public sealed class FileService(AppDbContext dbContext, IFileStorage fileStorage, IOptions<StorageOptions> storageOptions)
{
    private const long MaximumSizeBytes = 10 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, FilePolicy> Policies = new Dictionary<string, FilePolicy>(StringComparer.OrdinalIgnoreCase)
    {
        ["cv"] = new(10 * 1024 * 1024, new Dictionary<string, FileSignature>(StringComparer.OrdinalIgnoreCase) { [".pdf"] = FileSignature.Pdf }),
        ["image"] = new(10 * 1024 * 1024, new Dictionary<string, FileSignature>(StringComparer.OrdinalIgnoreCase) { [".jpg"] = FileSignature.Jpeg, [".jpeg"] = FileSignature.Jpeg, [".png"] = FileSignature.Png, [".webp"] = FileSignature.Webp }),
        ["submission"] = new(50 * 1024 * 1024, new Dictionary<string, FileSignature>(StringComparer.OrdinalIgnoreCase) { [".pdf"] = FileSignature.Pdf, [".docx"] = FileSignature.OfficeOpenXml, [".zip"] = FileSignature.Zip, [".pptx"] = FileSignature.OfficeOpenXml })
    };

    public async Task<FileUploadRequestResponse?> CreateUploadRequestAsync(Guid userId, CreateFileUploadRequest request, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        var policy = Policies.GetValueOrDefault(request.Category.ToLowerInvariant());
        if (!storageOptions.Value.Enabled || policy is null || request.SizeBytes <= 0 || request.SizeBytes > policy.MaximumSizeBytes || !policy.Extensions.ContainsKey(extension) || !string.Equals(request.ContentType, GetContentType(extension), StringComparison.OrdinalIgnoreCase)) return null;
        if (request.FileName.Contains('/') || request.FileName.Contains('\\') || Path.GetFileName(request.FileName) != request.FileName) return null;
        if (request.ProjectId.HasValue && !await HasProjectScopeAsync(userId, request.ProjectId.Value, cancellationToken)) return null;

        var file = new FileRecord(userId, request.ProjectId, request.FileName, $"uploads/{Guid.CreateVersion7():N}{extension}", request.ContentType, request.SizeBytes, DateTimeOffset.UtcNow.AddMinutes(storageOptions.Value.UploadUrlMinutes));
        dbContext.FileRecords.Add(file);
        await dbContext.SaveChangesAsync(cancellationToken);
        var url = await fileStorage.CreateUploadUrlAsync(file.StorageKey, file.ContentType, TimeSpan.FromMinutes(storageOptions.Value.UploadUrlMinutes), cancellationToken);
        return new FileUploadRequestResponse(Map(file), url);
    }

    public async Task<FileRecordResponse?> CompleteAsync(Guid userId, Guid fileId, CompleteFileUploadRequest request, CancellationToken cancellationToken)
    {
        var file = await dbContext.FileRecords.SingleOrDefaultAsync(item => item.Id == fileId && item.UploadedByUserId == userId, cancellationToken);
        if (file is null) return null;
        var extension = Path.GetExtension(file.OriginalFileName).ToLowerInvariant();
        var policy = Policies.Values.FirstOrDefault(item => item.Extensions.ContainsKey(extension) && string.Equals(file.ContentType, GetContentType(extension), StringComparison.OrdinalIgnoreCase));
        var metadata = await fileStorage.GetMetadataAsync(file.StorageKey, cancellationToken);
        if (policy is null || metadata is null || metadata.Length != file.ExpectedSizeBytes || !string.Equals(metadata.ChecksumSha256, request.ChecksumSha256, StringComparison.OrdinalIgnoreCase) || metadata.Signature != policy.Extensions[extension] || !string.Equals(metadata.DetectedContentType, file.ContentType, StringComparison.OrdinalIgnoreCase)) return null;
        try { file.Complete(request.ChecksumSha256, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException) { return null; }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(file);
    }

    public async Task<FileRecordResponse?> GetAsync(Guid userId, Guid fileId, CancellationToken cancellationToken)
    {
        var file = await dbContext.FileRecords.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fileId, cancellationToken);
        return file is not null && await CanAccessAsync(userId, file, cancellationToken) ? Map(file) : null;
    }

    public async Task<Uri?> DownloadAsync(Guid userId, Guid fileId, CancellationToken cancellationToken)
    {
        var file = await dbContext.FileRecords.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fileId && item.Status == FileUploadStatus.COMPLETED, cancellationToken);
        return file is not null && await CanAccessAsync(userId, file, cancellationToken)
            ? await fileStorage.CreateDownloadUrlAsync(file.StorageKey, TimeSpan.FromMinutes(storageOptions.Value.DownloadUrlMinutes), cancellationToken)
            : null;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid fileId, CancellationToken cancellationToken)
    {
        var file = await dbContext.FileRecords.SingleOrDefaultAsync(item => item.Id == fileId && item.Status != FileUploadStatus.DELETED, cancellationToken);
        if (file is null || !await CanAccessAsync(userId, file, cancellationToken)) return false;
        file.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Removes expired pending objects and marks their metadata expired. Intended for scheduled-worker invocation.</summary>
    public async Task<int> ExpirePendingUploadsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expired = await dbContext.FileRecords.Where(file => file.Status == FileUploadStatus.PENDING && file.ExpiresAt <= now).ToListAsync(cancellationToken);
        foreach (var file in expired)
        {
            await fileStorage.DeleteAsync(file.StorageKey, cancellationToken);
            file.Expire(now);
        }

        if (expired.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    private async Task<bool> HasProjectScopeAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken) ||
        await dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member => member.UserId == userId), cancellationToken) ||
        await dbContext.LecturerAssignments.Join(dbContext.LecturerProfiles, assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer }).AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == DNTU.SkillBridge.Domain.Lecturers.LecturerAssignmentStatus.ACTIVE && row.lecturer.UserId == userId, cancellationToken);

    private async Task<bool> CanAccessAsync(Guid userId, FileRecord file, CancellationToken cancellationToken) => file.UploadedByUserId == userId || file.ProjectId.HasValue && await HasProjectScopeAsync(userId, file.ProjectId.Value, cancellationToken);
    private static FileRecordResponse Map(FileRecord file) => new(file.Id, file.ProjectId, file.OriginalFileName, file.ContentType, file.ExpectedSizeBytes, file.Status, file.ExpiresAt, file.CompletedAt);
    private static string GetContentType(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".zip" => "application/zip",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => string.Empty
    };

    private sealed record FilePolicy(long MaximumSizeBytes, IReadOnlyDictionary<string, FileSignature> Extensions);
}
