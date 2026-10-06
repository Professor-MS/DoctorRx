using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;

namespace DoctorRx.Domain.Interfaces;

public interface IPatientRepository : IRepository<Patient>
{
    Task<IReadOnlyList<Patient>> SearchAsync(string query, int maxResults = 50, bool showArchived = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Patient>> GetRecentPatientsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<Patient?> GetWithPrescriptionsAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Patient>> GetPagedAsync(int pageNumber, int pageSize, bool showArchived = false, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(bool showArchived = false, CancellationToken cancellationToken = default);
    Task<Patient?> FindDuplicateAsync(string normalizedName, string? phoneDigits, CancellationToken cancellationToken = default);
}
