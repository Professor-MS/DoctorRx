using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DoctorRx.Infrastructure.Repositories;

public class MedicineRepository : Repository<Medicine>, IMedicineRepository
{
    public MedicineRepository(DoctorRxDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Medicine>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var cleanQuery = query.Trim().ToLower();

        return await DbSet
            .AsNoTracking()
            .Where(m => m.IsActive &&
                       (m.Name.ToLower().Contains(cleanQuery) ||
                       (m.GenericName != null && m.GenericName.ToLower().Contains(cleanQuery))))
            .OrderBy(m => m.Name)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
    }
}

public class DoctorRepository : Repository<Doctor>, IDoctorRepository
{
    public DoctorRepository(DoctorRxDbContext context) : base(context)
    {
    }

    public async Task<Doctor?> GetActiveDoctorAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(d => d.IsActive, cancellationToken);
    }
}
