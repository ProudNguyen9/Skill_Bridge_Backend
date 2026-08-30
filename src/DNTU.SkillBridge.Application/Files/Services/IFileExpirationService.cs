namespace DNTU.SkillBridge.Application.Files;

/// <summary>
/// Scheduled-worker boundary for expiring pending uploads. Implemented by the file
/// service so the cleanup background worker does not depend on the HTTP host layer.
/// </summary>
public interface IFileExpirationService
{
    Task<int> ExpirePendingUploadsAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
