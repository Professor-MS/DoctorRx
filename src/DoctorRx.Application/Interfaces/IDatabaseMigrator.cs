using System.Threading;
using System.Threading.Tasks;

namespace DoctorRx.Application.Interfaces;

public interface IDatabaseMigrator
{
    Task MigrateDatabaseAsync(CancellationToken cancellationToken = default);
}
