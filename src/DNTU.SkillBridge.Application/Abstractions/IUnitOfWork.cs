namespace DNTU.SkillBridge.Application.Abstractions;

/// <summary>
/// Commits pending changes made through repositories. Implemented by the
/// infrastructure layer on top of the EF Core change tracker.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Begins an explicit database transaction spanning the pending changes of all repositories.</summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
