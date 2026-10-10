using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;

namespace DoctorRx.Domain.Interfaces;

public interface IMedicineRepository : IRepository<Medicine>
{
    Task<IReadOnlyList<Medicine>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Medicine>> SearchAsync(string query, string? dosageForm, bool includeInactive = false, int maxResults = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Medicine>> FindPotentialDuplicatesAsync(string name, string form, string? strength, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<int> GetPrescriptionReferenceCountAsync(int medicineId, CancellationToken cancellationToken = default);
    Task<int> GetDraftReferenceCountAsync(int medicineId, CancellationToken cancellationToken = default);
}
