using System.Threading;
using System.Threading.Tasks;

namespace DoctorRx.Application.Interfaces;

public interface IDemoDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
