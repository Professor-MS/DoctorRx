using System;
using System.Threading;
using System.Threading.Tasks;

namespace DoctorRx.Domain.Interfaces;

public interface IDbTransactionScope : IAsyncDisposable, IDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
