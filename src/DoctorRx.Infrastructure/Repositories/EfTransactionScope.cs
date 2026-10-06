using System;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace DoctorRx.Infrastructure.Repositories;

public class EfTransactionScope : IDbTransactionScope
{
    private readonly IDbContextTransaction _transaction;
    private bool _isCompleted;

    public EfTransactionScope(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken);
        _isCompleted = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.RollbackAsync(cancellationToken);
        _isCompleted = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_isCompleted)
        {
            try
            {
                await _transaction.RollbackAsync();
            }
            catch
            {
                // Suppress rollback errors on dispose
            }
        }
        await _transaction.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (!_isCompleted)
        {
            try
            {
                _transaction.Rollback();
            }
            catch
            {
                // Suppress rollback errors on dispose
            }
        }
        _transaction.Dispose();
        GC.SuppressFinalize(this);
    }
}
