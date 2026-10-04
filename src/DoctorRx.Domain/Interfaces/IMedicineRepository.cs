using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;

namespace DoctorRx.Domain.Interfaces;

public interface IMedicineRepository : IRepository<Medicine>
{
    Task<IReadOnlyList<Medicine>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default);
}
