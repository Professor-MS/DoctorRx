using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;

namespace DoctorRx.Domain.Interfaces;

public interface IDoctorRepository : IRepository<Doctor>
{
    Task<Doctor?> GetActiveDoctorAsync(CancellationToken cancellationToken = default);
}
