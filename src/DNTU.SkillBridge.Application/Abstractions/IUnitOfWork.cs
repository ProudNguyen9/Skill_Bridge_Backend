namespace DNTU.SkillBridge.Application.Abstractions;

/// <summary>
/// Commits pending changes made through repositories. Implemented by the
/// infrastructure layer on top of the EF Core change tracker.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
