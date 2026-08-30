using System.Data;
using DNTU.SkillBridge.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DNTU.SkillBridge.Infrastructure.Persistence;

/// <summary>EF Core implementation of the unit of work over the application DbContext.</summary>
public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new PersistenceException("Failed to save changes.", exception);
        }
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new UnitOfWorkTransaction(transaction);
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        return new UnitOfWorkTransaction(transaction);
    }

    private sealed class UnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                throw new PersistenceException("Failed to commit the transaction.", exception);
            }
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

        public async ValueTask DisposeAsync() => await transaction.DisposeAsync();
    }
}
