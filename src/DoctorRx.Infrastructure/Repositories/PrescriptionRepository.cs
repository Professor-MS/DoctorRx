using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DoctorRx.Infrastructure.Repositories;

public class PrescriptionRepository : Repository<Prescription>, IPrescriptionRepository
{
    public PrescriptionRepository(DoctorRxDbContext context) : base(context)
    {
    }

    public override Task DeleteAsync(Prescription entity, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Prescriptions are immutable medical records and can never be deleted.");
    }

    public async Task<Prescription?> GetDetailedAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Prescription>> GetRecentPrescriptionsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(p => p.Items)
            .OrderByDescending(p => p.PrescriptionDate)
            .ThenByDescending(p => p.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Prescription>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(p => p.Items)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.PrescriptionDate)
            .ThenByDescending(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(p => p.PrescriptionDate == date, cancellationToken);
    }

    public async Task<string> GenerateNextPrescriptionNumberAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var prefix = $"RX-{today:yyyyMMdd}-";

        var todayCount = await DbSet.CountAsync(p => p.PrescriptionDate == today, cancellationToken);
        var sequence = todayCount + 1;

        var candidateNumber = $"{prefix}{sequence:D4}";

        while (await DbSet.AnyAsync(p => p.PrescriptionNumber == candidateNumber, cancellationToken))
        {
            sequence++;
            candidateNumber = $"{prefix}{sequence:D4}";
        }

        return candidateNumber;
    }
}
