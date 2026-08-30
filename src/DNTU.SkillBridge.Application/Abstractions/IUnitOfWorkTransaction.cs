namespace DNTU.SkillBridge.Application.Abstractions;

/// <summary>
/// An explicit database transaction begun through <see cref="IUnitOfWork"/>.
/// Disposing the transaction without committing rolls it back, matching EF Core semantics.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
