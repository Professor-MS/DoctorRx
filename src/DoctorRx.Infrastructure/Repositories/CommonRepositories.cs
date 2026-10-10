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
        var added = await base.AddAsync(medicine, cancellationToken);
        if (medicine.IsActive)
        {
            foreach (var token in medicine.SearchTokens)
            {
                token.MedicineId = added.Id;
                token.Medicine = added;
                await Context.MedicineSearchTokens.AddAsync(token, cancellationToken);
            }
        }
        return added;
    }

    public override async Task UpdateAsync(Medicine medicine, CancellationToken cancellationToken = default)
    {
        var existingTokens = await Context.MedicineSearchTokens
            .Where(t => t.MedicineId == medicine.Id)
            .ToListAsync(cancellationToken);
        Context.MedicineSearchTokens.RemoveRange(existingTokens);

        medicine.RefreshSearchFields();
        if (medicine.IsActive)
        {
            foreach (var token in medicine.SearchTokens)
            {
                token.MedicineId = medicine.Id;
                await Context.MedicineSearchTokens.AddAsync(token, cancellationToken);
            }
        }

        await base.UpdateAsync(medicine, cancellationToken);
    }

    public override async Task DeleteAsync(Medicine medicine, CancellationToken cancellationToken = default)
    {
        var existingTokens = await Context.MedicineSearchTokens
            .Where(t => t.MedicineId == medicine.Id)
            .ToListAsync(cancellationToken);
        Context.MedicineSearchTokens.RemoveRange(existingTokens);
        await base.DeleteAsync(medicine, cancellationToken);
    }

    public Task<IReadOnlyList<Medicine>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        return SearchAsync(query, dosageForm: null, includeInactive: false, maxResults, cancellationToken);
    }

    public async Task<IReadOnlyList<Medicine>> SearchAsync(string query, string? dosageForm, bool includeInactive = false, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var cleanQuery = SearchNormalizer.Normalize(query);
        var formFilter = string.IsNullOrWhiteSpace(dosageForm) || dosageForm.Equals("All Forms", StringComparison.OrdinalIgnoreCase)
            ? null
            : dosageForm.Trim();

        IQueryable<Medicine> baseQuery = DbSet.AsNoTracking();
        if (!includeInactive)
        {
            baseQuery = baseQuery.Where(m => m.IsActive);
        }
        if (formFilter != null)
        {
            if (formFilter.Equals("Drops", StringComparison.OrdinalIgnoreCase))
                baseQuery = baseQuery.Where(m => EF.Functions.Like(m.Form, "%Drop%"));
            else if (formFilter.Equals("Cream / Ointment", StringComparison.OrdinalIgnoreCase))
                baseQuery = baseQuery.Where(m => EF.Functions.Like(m.Form, "%Cream%") || EF.Functions.Like(m.Form, "%Ointment%"));
            else
                baseQuery = baseQuery.Where(m => m.Form.ToLower() == formFilter.ToLower());
        }

        if (string.IsNullOrWhiteSpace(cleanQuery))
        {
            return await baseQuery
                .OrderByDescending(m => m.UsageCount)
                .ThenBy(m => m.NormalizedName)
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }

        var parsedTokens = SearchNormalizer.ParseQueryTokens(query);
        if (parsedTokens.Count == 0)
        {
            var prefixPattern = $"{cleanQuery}%";
            return await baseQuery
                .Where(m => (EF.Functions.Like(m.NormalizedName, prefixPattern) ||
                            (m.GenericName != null && EF.Functions.Like(m.GenericName, prefixPattern))))
                .OrderByDescending(m => m.UsageCount)
                .ThenBy(m => m.NormalizedName)
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }

        // Multi-token intersection across MedicineSearchTokens index
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

        var candidateIds = await matchingIdsQuery!
            .Distinct()
            .Take(maxResults * 2)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            // Fallback for untokenized records: direct prefix matching
            var cleanPrefixPattern = $"{cleanQuery}%";
            candidateIds = await baseQuery
                .Where(m => (EF.Functions.Like(m.NormalizedName, cleanPrefixPattern) ||
                            (m.GenericName != null && EF.Functions.Like(m.GenericName, cleanPrefixPattern))))
                .Select(m => m.Id)
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }

        if (candidateIds.Count == 0)
        {
            return Array.Empty<Medicine>();
        }

        var matched = await baseQuery
            .Where(m => candidateIds.Contains(m.Id))
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

    public async Task<IReadOnlyList<Medicine>> FindPotentialDuplicatesAsync(string name, string form, string? strength, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var cleanName = SearchNormalizer.Normalize(name);
        if (string.IsNullOrWhiteSpace(cleanName)) return Array.Empty<Medicine>();

        var cleanForm = (form ?? string.Empty).Trim();
        var cleanStrength = (strength ?? string.Empty).Trim();

        var candidates = await DbSet
            .AsNoTracking()
            .Where(m => (excludeId == null || m.Id != excludeId.Value) &&
                        (m.NormalizedName == cleanName || EF.Functions.Like(m.Name, name.Trim())))
            .ToListAsync(cancellationToken);

        return candidates
            .Where(m => string.Equals(m.Form.Trim(), cleanForm, StringComparison.OrdinalIgnoreCase) &&
                        IsStrengthMatch(m.Strength, cleanStrength))
            .ToList();
    }

    public async Task<int> GetPrescriptionReferenceCountAsync(int medicineId, CancellationToken cancellationToken = default)
    {
        return await Context.PrescriptionMedicines
            .CountAsync(pm => pm.MedicineId == medicineId, cancellationToken);
    }

    public async Task<int> GetDraftReferenceCountAsync(int medicineId, CancellationToken cancellationToken = default)
    {
        var drafts = await Context.Drafts
            .AsNoTracking()
            .Select(d => d.PayloadJson)
            .ToListAsync(cancellationToken);

        var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        int count = 0;
        foreach (var payload in drafts)
        {
            if (string.IsNullOrWhiteSpace(payload)) continue;
            try
            {
                var state = System.Text.Json.JsonSerializer.Deserialize<DoctorRx.Application.DTOs.PrescriptionComposerState>(payload, jsonOptions);
                if (state?.Items != null && state.Items.Any(i => i.MedicineId == medicineId))
                {
                    count++;
                }
            }
            catch
            {
                if (payload.Contains($"\"MedicineId\":{medicineId}", StringComparison.OrdinalIgnoreCase) ||
                    payload.Contains($"\"MedicineId\": {medicineId}", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static bool IsStrengthMatch(string? dbStrength, string inputStrength)
    {
        var s1 = System.Text.RegularExpressions.Regex.Replace((dbStrength ?? string.Empty).Trim(), @"\s+", "").ToLowerInvariant();
        var s2 = System.Text.RegularExpressions.Regex.Replace((inputStrength ?? string.Empty).Trim(), @"\s+", "").ToLowerInvariant();
        return string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
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
