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

public class DraftRepository : Repository<Draft>, IDraftRepository
{
    public DraftRepository(DoctorRxDbContext context) : base(context)
    {
    }

    public async Task<Draft?> GetByDraftKeyAsync(Guid draftKey, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(d => d.Patient)
            .FirstOrDefaultAsync(d => d.DraftKey == draftKey, cancellationToken);
    }

    public async Task<IReadOnlyList<Draft>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(d => d.Patient)
            .OrderByDescending(d => d.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(cancellationToken);
    }
}
