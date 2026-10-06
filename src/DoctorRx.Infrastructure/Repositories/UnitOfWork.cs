using System;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

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

    public async Task<IDbTransactionScope> BeginWriteTransactionAsync(CancellationToken cancellationToken = default)
    {
        var rawConn = _context.Database.GetDbConnection();
        if (rawConn is SqliteConnection sqliteConn)
        {
            if (sqliteConn.State != System.Data.ConnectionState.Open)
            {
                await sqliteConn.OpenAsync(cancellationToken);
            }
            var sqliteTx = sqliteConn.BeginTransaction(deferred: false);
            var efTx = await _context.Database.UseTransactionAsync(sqliteTx, cancellationToken);
            if (efTx is null)
            {
                throw new InvalidOperationException("Failed to bind SQLite transaction to EF Core DbContext.");
            }
            return new EfTransactionScope(efTx);
        }

        var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new EfTransactionScope(tx);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
