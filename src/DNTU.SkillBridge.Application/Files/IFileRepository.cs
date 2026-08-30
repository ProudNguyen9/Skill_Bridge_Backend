using DNTU.SkillBridge.Domain.Files;

namespace DNTU.SkillBridge.Application.Files;

public interface IFileRepository
{
    void Add(FileRecord file);
    Task<FileRecord?> FindOwnedAsync(Guid userId, Guid fileId, CancellationToken cancellationToken);
    Task<FileRecord?> FindAsync(Guid fileId, CancellationToken cancellationToken);
    Task<FileRecord?> FindCompletedAsync(Guid fileId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FileRecord>> ListExpiredPendingAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> HasProjectScopeAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);
}
