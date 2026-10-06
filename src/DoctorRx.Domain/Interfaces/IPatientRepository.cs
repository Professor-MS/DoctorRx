using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;

namespace DoctorRx.Domain.Interfaces;

public interface IPatientRepository : IRepository<Patient>
{
    Task<IReadOnlyList<Patient>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Patient>> GetRecentPatientsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<Patient?> GetWithPrescriptionsAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Patient>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
}
