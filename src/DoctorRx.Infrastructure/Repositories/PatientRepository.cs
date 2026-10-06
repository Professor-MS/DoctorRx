using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
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

    public async Task<IReadOnlyList<Patient>> SearchAsync(string query, int maxResults = 50, bool showArchived = false, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = SearchNormalizer.Normalize(query);
        var phoneDigits = SearchNormalizer.NormalizePhoneDigits(query);

        var dbQuery = DbSet.AsNoTracking();
        if (!showArchived)
        {
            dbQuery = dbQuery.Where(p => !p.IsArchived);
        }

        return await dbQuery
            .Where(p => p.NormalizedName.Contains(normalizedQuery) ||
                        (!string.IsNullOrEmpty(phoneDigits) && p.PhoneDigits != null && p.PhoneDigits.Contains(phoneDigits)))
            .OrderByDescending(p => p.Id)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Patient>> GetRecentPatientsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(p => !p.IsArchived)
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

    public async Task<IReadOnlyList<Patient>> GetPagedAsync(int pageNumber, int pageSize, bool showArchived = false, CancellationToken cancellationToken = default)
    {
        var dbQuery = DbSet.AsNoTracking();
        if (!showArchived)
        {
            dbQuery = dbQuery.Where(p => !p.IsArchived);
        }

        return await dbQuery
            .OrderByDescending(p => p.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(bool showArchived = false, CancellationToken cancellationToken = default)
    {
        if (showArchived)
        {
            return await DbSet.CountAsync(cancellationToken);
        }
        return await DbSet.CountAsync(p => !p.IsArchived, cancellationToken);
    }

    public async Task<Patient?> FindDuplicateAsync(string normalizedName, string? phoneDigits, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneDigits))
        {
            return null;
        }

        return await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(p => !p.IsArchived && p.NormalizedName == normalizedName && p.PhoneDigits == phoneDigits, cancellationToken);
    }
}
