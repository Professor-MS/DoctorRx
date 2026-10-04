using System;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;

namespace DoctorRx.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly DoctorRxDbContext _context;

    public IPatientRepository Patients { get; }
    public IPrescriptionRepository Prescriptions { get; }
    public IMedicineRepository Medicines { get; }
    public IDoctorRepository Doctors { get; }

    public UnitOfWork(
        DoctorRxDbContext context,
        IPatientRepository patients,
        IPrescriptionRepository prescriptions,
        IMedicineRepository medicines,
        IDoctorRepository doctors)
    {
        _context = context;
        Patients = patients;
        Prescriptions = prescriptions;
        Medicines = medicines;
        Doctors = doctors;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
