using System;
using System.Threading;
using System.Threading.Tasks;

namespace DoctorRx.Domain.Interfaces;

public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
    IPatientRepository Patients { get; }
    IPrescriptionRepository Prescriptions { get; }
    IMedicineRepository Medicines { get; }
    IDoctorRepository Doctors { get; }

    Task<int> CommitAsync(CancellationToken cancellationToken = default);
    Task<IDbTransactionScope> BeginWriteTransactionAsync(CancellationToken cancellationToken = default);
}
