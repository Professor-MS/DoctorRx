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

public class MedicineRepository : Repository<Medicine>, IMedicineRepository
{
    public MedicineRepository(DoctorRxDbContext context) : base(context)
    {
    }

    public override async Task<Medicine> AddAsync(Medicine medicine, CancellationToken cancellationToken = default)
    {
        medicine.RefreshSearchFields();
        return await base.AddAsync(medicine, cancellationToken);
    }

    public override async Task UpdateAsync(Medicine medicine, CancellationToken cancellationToken = default)
    {
        var existingTokens = await Context.MedicineSearchTokens
            .Where(t => t.MedicineId == medicine.Id)
            .ToListAsync(cancellationToken);
        Context.MedicineSearchTokens.RemoveRange(existingTokens);

        medicine.RefreshSearchFields();
        foreach (var token in medicine.SearchTokens)
        {
            await Context.MedicineSearchTokens.AddAsync(token, cancellationToken);
        }

        await base.UpdateAsync(medicine, cancellationToken);
    }

    public async Task<IReadOnlyList<Medicine>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var cleanQuery = SearchNormalizer.Normalize(query);
        if (string.IsNullOrWhiteSpace(cleanQuery))
        {
            return await DbSet
                .AsNoTracking()
                .Where(m => m.IsActive)
                .OrderByDescending(m => m.UsageCount)
                .ThenBy(m => m.NormalizedName)
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }

        var parsedTokens = SearchNormalizer.ParseQueryTokens(query);
        var hasTokens = await Context.MedicineSearchTokens.AnyAsync(cancellationToken);

        if (!hasTokens || parsedTokens.Count == 0)
        {
            var prefixPattern = $"{cleanQuery}%";
            return await DbSet
                .AsNoTracking()
                .Where(m => m.IsActive &&
                           (EF.Functions.Like(m.NormalizedName, prefixPattern) ||
                           (m.GenericName != null && EF.Functions.Like(m.GenericName, prefixPattern))))
                .OrderByDescending(m => m.UsageCount)
                .ThenBy(m => m.NormalizedName)
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }

        // Multi-token intersection across MedicineSearchTokens
        IQueryable<int>? matchingIdsQuery = null;
        foreach (var token in parsedTokens)
        {
            var prefix = $"{token.NormalizedToken}%";
            var tokenIds = Context.MedicineSearchTokens
                .Where(x => EF.Functions.Like(x.Token, prefix))
                .Select(x => x.MedicineId);

            matchingIdsQuery = matchingIdsQuery == null
                ? tokenIds
                : matchingIdsQuery.Intersect(tokenIds);
        }

        var cleanPrefixPattern = $"{cleanQuery}%";
        var directPrefixIds = DbSet
            .Where(m => m.IsActive &&
                       (EF.Functions.Like(m.NormalizedName, cleanPrefixPattern) ||
                       (m.GenericName != null && EF.Functions.Like(m.GenericName, cleanPrefixPattern))))
            .Select(m => m.Id);

        var candidateIdsQuery = matchingIdsQuery != null
            ? matchingIdsQuery.Union(directPrefixIds)
            : directPrefixIds;

        var candidateIds = await candidateIdsQuery
            .Distinct()
            .Take(maxResults * 3)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return Array.Empty<Medicine>();
        }

        var matched = await DbSet
            .AsNoTracking()
            .Where(m => m.IsActive && candidateIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

        var firstToken = parsedTokens[0].NormalizedToken;
        return matched
            .OrderByDescending(m => m.UsageCount)
            .ThenBy(m => m.NormalizedName == cleanQuery ? 0 : 1)
            .ThenBy(m => m.NormalizedName.StartsWith(firstToken, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(m => m.NormalizedName)
            .Take(maxResults)
            .ToList();
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
