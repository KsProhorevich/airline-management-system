using AirlineApi.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace AirlineApi.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;

    public UnitOfWork(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        return new EfAppTransaction(transaction);
    }

    private sealed class EfAppTransaction : IAppTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfAppTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken) =>
            _transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken) =>
            _transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
