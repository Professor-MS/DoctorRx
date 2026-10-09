using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DoctorRx.Infrastructure.Repositories;

public class PatientRepository : Repository<Patient>, IPatientRepository
{
    public PatientRepository(DoctorRxDbContext context) : base(context)
    {
    }

    public override async Task<Patient> AddAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        patient.RefreshSearchFields();
        return await base.AddAsync(patient, cancellationToken);
    }

    public override async Task UpdateAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        // Replace search tokens inside the same UnitOfWork transaction
        var existingTokens = await Context.PatientSearchTokens
            .Where(t => t.PatientId == patient.Id)
            .ToListAsync(cancellationToken);
        Context.PatientSearchTokens.RemoveRange(existingTokens);

        patient.RefreshSearchFields();
        foreach (var token in patient.SearchTokens)
        {
            await Context.PatientSearchTokens.AddAsync(token, cancellationToken);
        }

        await base.UpdateAsync(patient, cancellationToken);
    }

    public async Task<IReadOnlyList<Patient>> SearchAsync(string query, int maxResults = 50, bool showArchived = false, CancellationToken cancellationToken = default)
    {
        var dbQuery = DbSet.AsNoTracking();
        if (!showArchived)
        {
            dbQuery = dbQuery.Where(p => !p.IsArchived);
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            // Empty query in search context: return most recently visited/created
            return await dbQuery
                .OrderByDescending(p => p.LastVisitDate)
                .ThenByDescending(p => p.Id)
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }

        var normalizedQuery = SearchNormalizer.Normalize(query);
        var parsedTokens = SearchNormalizer.ParseQueryTokens(query);

        if (parsedTokens.Count == 0)
        {
            return Array.Empty<Patient>();
        }

        // Tier 1: Exact full-name match via index
        var exactMatches = await dbQuery
            .Where(p => p.NormalizedName == normalizedQuery)
            .OrderByDescending(p => p.LastVisitDate)
            .ThenBy(p => p.Id)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        if (exactMatches.Count >= maxResults)
        {
            return exactMatches;
        }

        // Tier 2: Check if search token table has data
        var hasSearchTokens = await Context.PatientSearchTokens.AnyAsync(cancellationToken);

        if (!hasSearchTokens)
        {
            // Fallback while indexing is incomplete (Amendment 2)
            var phoneDigits = SearchNormalizer.NormalizePhoneDigits(query);
            var prefixPattern = $"{normalizedQuery}%";
            var phonePrefix = !string.IsNullOrEmpty(phoneDigits) ? $"{phoneDigits}%" : null;

            return await dbQuery
                .Where(p => EF.Functions.Like(p.NormalizedName, prefixPattern) ||
                            (phonePrefix != null && p.PhoneDigits != null && EF.Functions.Like(p.PhoneDigits, phonePrefix)))
                .OrderBy(p => p.NormalizedName)
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }

        // Tier 3: Multi-token intersection across Token table
        IQueryable<int>? matchingPatientIdsQuery = null;

        foreach (var token in parsedTokens)
        {
            IQueryable<int> currentTokenQuery;

            if (token.IsRecordNumberPrefix && token.RecordNumberDigits != null)
            {
                var recordDigits = token.RecordNumberDigits;
                var recordNorm = token.NormalizedToken;
                currentTokenQuery = Context.PatientSearchTokens
                    .Where(x => x.TokenType == SearchTokenType.RecordNumber &&
                               (x.Token == recordDigits || x.Token == recordNorm))
                    .Select(x => x.PatientId);
            }
            else if (token.IsDigitsOnly)
            {
                var digits = token.StrippedDigits;
                if (digits.Length >= 3)
                {
                    var prefix = $"{digits}%";
                    currentTokenQuery = Context.PatientSearchTokens
                        .Where(x => (x.TokenType == SearchTokenType.PhoneSuffix && EF.Functions.Like(x.Token, prefix)) ||
                                   (x.TokenType == SearchTokenType.RecordNumber && x.Token == digits))
                        .Select(x => x.PatientId);
                }
                else
                {
                    currentTokenQuery = Context.PatientSearchTokens
                        .Where(x => x.TokenType == SearchTokenType.RecordNumber && x.Token == digits)
                        .Select(x => x.PatientId);
                }
            }
            else
            {
                var prefix = $"{token.NormalizedToken}%";
                var norm = token.NormalizedToken;
                currentTokenQuery = Context.PatientSearchTokens
                    .Where(x => (x.TokenType == SearchTokenType.NameWord && EF.Functions.Like(x.Token, prefix)) ||
                               (x.TokenType == SearchTokenType.RecordNumber && x.Token == norm))
                    .Select(x => x.PatientId);
            }

            matchingPatientIdsQuery = matchingPatientIdsQuery == null
                ? currentTokenQuery
                : matchingPatientIdsQuery.Intersect(currentTokenQuery);
        }

        var directRecordIds = dbQuery
            .Where(p => EF.Functions.Like(p.RecordNumber, $"{normalizedQuery}%"))
            .Select(p => p.Id);

        var directNamePrefixIds = dbQuery
            .Where(p => EF.Functions.Like(p.NormalizedName, $"{normalizedQuery}%"))
            .Select(p => p.Id);

        var directCandidates = directRecordIds.Union(directNamePrefixIds);

        var candidateIdsQuery = matchingPatientIdsQuery != null
            ? matchingPatientIdsQuery.Union(directCandidates)
            : directCandidates;

        var candidateIds = await candidateIdsQuery
            .Distinct()
            .Take(maxResults * 3)
            .ToListAsync(cancellationToken);

        // Always retain exact matches in candidate pool
        foreach (var exact in exactMatches)
        {
            if (!candidateIds.Contains(exact.Id))
            {
                candidateIds.Add(exact.Id);
            }
        }

        if (candidateIds.Count == 0)
        {
            return exactMatches;
        }

        var matchedPatients = await dbQuery
            .Where(p => candidateIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        // Tiered Ranking:
        // 1. Exact full name match
        // 2. Name starts with first token
        // 3. Word-prefix matches
        // 4. Phone/record matches
        // Ties broken by LastVisitDate DESC, Name ASC, Id ASC
        var firstToken = parsedTokens[0].NormalizedToken;

        var ranked = matchedPatients
            .OrderBy(p => p.NormalizedName == normalizedQuery ? 0 : 1)
            .ThenBy(p => p.NormalizedName.StartsWith(firstToken, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenByDescending(p => p.LastVisitDate)
            .ThenBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Take(maxResults)
            .ToList();

        return ranked;
    }

    public async Task<IReadOnlyList<Patient>> GetRecentPatientsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(p => !p.IsArchived)
            .OrderByDescending(p => p.LastVisitDate)
            .ThenByDescending(p => p.Id)
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

    public async Task<(IReadOnlyList<Patient> Items, int TotalCount)> SearchFilteredPagedAsync(
        string? query,
        int statusFilter,
        int sortOption,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 50;

        var dbQuery = DbSet.AsNoTracking();

        // Status Filter: 0=Active, 1=Archived, 2=All
        dbQuery = statusFilter switch
        {
            0 => dbQuery.Where(p => !p.IsArchived),
            1 => dbQuery.Where(p => p.IsArchived),
            _ => dbQuery
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            var parsedTokens = SearchNormalizer.ParseQueryTokens(query);
            if (parsedTokens.Count > 0)
            {
                var hasSearchTokens = await Context.PatientSearchTokens.AnyAsync(cancellationToken);
                if (hasSearchTokens)
                {
                    IQueryable<int>? matchingPatientIdsQuery = null;

                    foreach (var token in parsedTokens)
                    {
                        IQueryable<int> currentTokenQuery;

                        if (token.IsRecordNumberPrefix && token.RecordNumberDigits != null)
                        {
                            var recordDigits = token.RecordNumberDigits;
                            var recordNorm = token.NormalizedToken;
                            currentTokenQuery = Context.PatientSearchTokens
                                .Where(x => x.TokenType == SearchTokenType.RecordNumber &&
                                           (x.Token == recordDigits || x.Token == recordNorm))
                                .Select(x => x.PatientId);
                        }
                        else if (token.IsDigitsOnly)
                        {
                            var digits = token.StrippedDigits;
                            if (digits.Length >= 3)
                            {
                                var prefix = $"{digits}%";
                                currentTokenQuery = Context.PatientSearchTokens
                                    .Where(x => (x.TokenType == SearchTokenType.PhoneSuffix && EF.Functions.Like(x.Token, prefix)) ||
                                               (x.TokenType == SearchTokenType.RecordNumber && x.Token == digits))
                                    .Select(x => x.PatientId);
                            }
                            else
                            {
                                currentTokenQuery = Context.PatientSearchTokens
                                    .Where(x => x.TokenType == SearchTokenType.RecordNumber && x.Token == digits)
                                    .Select(x => x.PatientId);
                            }
                        }
                        else
                        {
                            var prefix = $"{token.NormalizedToken}%";
                            var norm = token.NormalizedToken;
                            currentTokenQuery = Context.PatientSearchTokens
                                .Where(x => (x.TokenType == SearchTokenType.NameWord && EF.Functions.Like(x.Token, prefix)) ||
                                           (x.TokenType == SearchTokenType.RecordNumber && x.Token == norm))
                                .Select(x => x.PatientId);
                        }

                        matchingPatientIdsQuery = matchingPatientIdsQuery == null
                            ? currentTokenQuery
                            : matchingPatientIdsQuery.Intersect(currentTokenQuery);
                    }

                    var normQuery = SearchNormalizer.Normalize(query);
                    var directRecordIds = Context.Patients.Where(p => EF.Functions.Like(p.RecordNumber, $"{normQuery}%")).Select(p => p.Id);
                    var combinedIds = matchingPatientIdsQuery != null ? matchingPatientIdsQuery.Union(directRecordIds) : directRecordIds;
                    dbQuery = dbQuery.Where(p => combinedIds.Contains(p.Id));
                }
                else
                {
                    var normalizedQuery = SearchNormalizer.Normalize(query);
                    var phoneDigits = SearchNormalizer.NormalizePhoneDigits(query);
                    var prefixPattern = $"{normalizedQuery}%";
                    var phonePrefix = !string.IsNullOrEmpty(phoneDigits) ? $"{phoneDigits}%" : null;

                    dbQuery = dbQuery.Where(p => EF.Functions.Like(p.NormalizedName, prefixPattern) ||
                                                 (phonePrefix != null && p.PhoneDigits != null && EF.Functions.Like(p.PhoneDigits, phonePrefix)));
                }
            }
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        // Sorting: 0=NameAsc, 1=LastVisitDesc, 2=RecentlyAddedDesc (stable with Id tie-breaker)
        IOrderedQueryable<Patient> orderedQuery = sortOption switch
        {
            0 => dbQuery.OrderBy(p => p.Name).ThenBy(p => p.Id),
            1 => dbQuery.OrderByDescending(p => p.LastVisitDate).ThenByDescending(p => p.Id),
            2 => dbQuery.OrderByDescending(p => p.Id),
            _ => dbQuery.OrderBy(p => p.Name).ThenBy(p => p.Id)
        };

        var items = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
