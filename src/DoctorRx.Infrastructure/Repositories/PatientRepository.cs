using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DoctorRx.Infrastructure.Repositories;

public class PatientRepository : Repository<Patient>, IPatientRepository
{
    public PatientRepository(DoctorRxDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Patient>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var cleanQuery = query.Trim().ToLower();

        return await DbSet
            .AsNoTracking()
            .Include(p => p.Prescriptions)
            .Where(p => p.Name.ToLower().Contains(cleanQuery) ||
                        (p.Phone != null && p.Phone.Contains(cleanQuery)))
            .OrderByDescending(p => p.Id)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Patient>> GetRecentPatientsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(p => p.Prescriptions)
            .OrderByDescending(p => p.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<Patient?> GetWithPrescriptionsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.Prescriptions)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }
}
