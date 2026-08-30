using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Domain.Files;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Application.Files;

public interface IFileService
{
    Task<FileUploadRequestResponse?> CreateUploadRequestAsync(Guid userId, CreateFileUploadRequest request, CancellationToken cancellationToken);
    Task<FileRecordResponse?> CompleteAsync(Guid userId, Guid fileId, CompleteFileUploadRequest request, CancellationToken cancellationToken);
    Task<FileRecordResponse?> GetAsync(Guid userId, Guid fileId, CancellationToken cancellationToken);
    Task<Uri?> DownloadAsync(Guid userId, Guid fileId, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid userId, Guid fileId, CancellationToken cancellationToken);
}
