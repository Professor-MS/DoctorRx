using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;

namespace DoctorRx.Domain.Interfaces;

public interface IPrescriptionRepository : IRepository<Prescription>
{
    Task<Prescription?> GetDetailedAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Prescription>> GetRecentPrescriptionsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Prescription>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default);
    Task<int> GetCountForDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<string> GenerateNextPrescriptionNumberAsync(CancellationToken cancellationToken = default);
}
