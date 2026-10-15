using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Domain.Models;
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

    public async Task<string> GenerateNextPrescriptionNumberAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var key = $"RX-{targetDate:yyyyMMdd}";

        var seq = await Context.NumberSequences.FindAsync(new object[] { key }, cancellationToken);
        if (seq == null)
        {
            var maxExisting = await DbSet
                .Where(p => p.PrescriptionDate == targetDate && !p.PrescriptionNumber.Contains("-A"))
                .CountAsync(cancellationToken);

            seq = new NumberSequence
            {
                SequenceKey = key,
                CurrentValue = maxExisting + 1,
                UpdatedAtUtc = DateTime.UtcNow
            };
            await Context.NumberSequences.AddAsync(seq, cancellationToken);
        }
        else
        {
            seq.CurrentValue++;
            seq.UpdatedAtUtc = DateTime.UtcNow;
        }

        return $"{key}-{seq.CurrentValue:D4}";
    }

    public override async Task<Prescription> AddAsync(Prescription entity, CancellationToken cancellationToken = default)
    {
        var result = await base.AddAsync(entity, cancellationToken);
        var tokens = SearchNormalizer.GeneratePrescriptionTokens(entity);
        foreach (var token in tokens)
        {
            token.Prescription = entity;
            await Context.PrescriptionSearchTokens.AddAsync(token, cancellationToken);
        }
        return result;
    }

    public async Task ReplaceSearchTokensAsync(int prescriptionId, IEnumerable<PrescriptionSearchToken> tokens, CancellationToken cancellationToken = default)
    {
        var existing = await Context.PrescriptionSearchTokens
            .Where(t => t.PrescriptionId == prescriptionId)
            .ToListAsync(cancellationToken);

        Context.PrescriptionSearchTokens.RemoveRange(existing);

        foreach (var token in tokens)
        {
            token.PrescriptionId = prescriptionId;
            await Context.PrescriptionSearchTokens.AddAsync(token, cancellationToken);
        }
    }

    public async Task<Prescription?> GetReplacementPrescriptionAsync(int parentPrescriptionId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ParentPrescriptionId == parentPrescriptionId, cancellationToken);
    }

    public async Task<PrescriptionHistoryPagedResult> SearchPrescriptionsPagedAsync(PrescriptionSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var pageNumber = criteria.PageNumber < 1 ? 1 : criteria.PageNumber;
        var pageSize = criteria.PageSize < 1 ? 50 : criteria.PageSize;

        var dbQuery = await BuildFilterQueryAsync(criteria, cancellationToken);

        // Sorting with stable tie-breaker on Id
        IOrderedQueryable<Prescription> orderedQuery = criteria.SortOption switch
        {
            PrescriptionSortOption.NewestFirst => dbQuery.OrderByDescending(p => p.PrescriptionDate).ThenByDescending(p => p.Id),
            PrescriptionSortOption.OldestFirst => dbQuery.OrderBy(p => p.PrescriptionDate).ThenBy(p => p.Id),
            PrescriptionSortOption.PatientName => dbQuery.OrderBy(p => EF.Functions.Collate(p.PatientSnapshot.Name, "NOCASE")).ThenBy(p => p.Id),
            PrescriptionSortOption.PrescriptionNumber => dbQuery.OrderByDescending(p => p.PrescriptionNumber).ThenByDescending(p => p.Id),
            _ => dbQuery.OrderByDescending(p => p.PrescriptionDate).ThenByDescending(p => p.Id)
        };

        // Projected DTO query - no entity materialization or tracking
        var items = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PrescriptionHistoryItemDto(
                p.Id,
                p.PrescriptionNumber,
                p.AmendmentNumber,
                p.PrescriptionDate,
                p.PatientSnapshot.Name,
                p.Patient != null ? p.Patient.RecordNumber : string.Empty,
                p.Items.Count,
                p.Status,
                p.Status == PrescriptionStatus.Superseded
                    ? Context.Prescriptions.Where(sub => sub.ParentPrescriptionId == p.Id).Select(sub => sub.PrescriptionNumber).FirstOrDefault()
                    : null,
                p.FinalizedAtUtc
            ))
            .ToListAsync(cancellationToken);

        const int MaxCountCap = 10_000;
        var totalCountRaw = await dbQuery.Take(MaxCountCap + 1).CountAsync(cancellationToken);
        bool isCapped = totalCountRaw > MaxCountCap;
        int totalCount = isCapped ? MaxCountCap : totalCountRaw;

        return new PrescriptionHistoryPagedResult(items, totalCount, isCapped, pageNumber, pageSize);
    }

    public async Task<int> GetPrescriptionCountAsync(PrescriptionSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var dbQuery = await BuildFilterQueryAsync(criteria, cancellationToken);
        const int MaxCountCap = 10_000;
        var raw = await dbQuery.Take(MaxCountCap + 1).CountAsync(cancellationToken);
        return Math.Min(raw, MaxCountCap);
    }

    private async Task<IQueryable<Prescription>> BuildFilterQueryAsync(PrescriptionSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var dbQuery = DbSet.AsNoTracking();

        // 1. Date filter (presets or custom)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly? fromDate = criteria.FromDate;
        DateOnly? toDate = criteria.ToDate;

        switch (criteria.DatePreset)
        {
            case PrescriptionDatePreset.Today:
                fromDate = today;
                toDate = today;
                break;
            case PrescriptionDatePreset.Last7Days:
                fromDate = today.AddDays(-6);
                toDate = today;
                break;
            case PrescriptionDatePreset.ThisMonth:
                fromDate = new DateOnly(today.Year, today.Month, 1);
                toDate = today;
                break;
            case PrescriptionDatePreset.Custom:
                break;
            case PrescriptionDatePreset.All:
            default:
                break;
        }

        if (fromDate.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.PrescriptionDate >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.PrescriptionDate <= toDate.Value);
        }

        // 2. Status filter
        switch (criteria.StatusFilter)
        {
            case PrescriptionStatusSearchFilter.Default:
                if (!criteria.IncludeReplaced)
                {
                    dbQuery = dbQuery.Where(p => p.Status != PrescriptionStatus.Superseded);
                }
                break;
            case PrescriptionStatusSearchFilter.Issued:
                dbQuery = dbQuery.Where(p => p.Status == PrescriptionStatus.Finalized);
                break;
            case PrescriptionStatusSearchFilter.Cancelled:
                dbQuery = dbQuery.Where(p => p.Status == PrescriptionStatus.Cancelled);
                break;
            case PrescriptionStatusSearchFilter.Replaced:
                dbQuery = dbQuery.Where(p => p.Status == PrescriptionStatus.Superseded);
                break;
            case PrescriptionStatusSearchFilter.All:
                break;
        }

        // 3. Patient ID filter
        if (criteria.PatientId.HasValue && criteria.PatientId.Value > 0)
        {
            dbQuery = dbQuery.Where(p => p.PatientId == criteria.PatientId.Value);
        }

        // 4. Text search
        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var parsedTokens = SearchNormalizer.ParseQueryTokens(criteria.SearchText);
            if (parsedTokens.Count > 0)
            {
                var meta = await Context.AppMetas.FirstOrDefaultAsync(m => m.Key == "PrescriptionSearchIndexVersion", cancellationToken);
                bool isIndexAvailable = meta?.Value == "1";

                if (isIndexAvailable)
                {
                    foreach (var token in parsedTokens)
                    {
                        if (token.IsRxNumberPrefix && token.RxNumberWithoutPrefix != null)
                        {
                            var fullNorm = token.NormalizedToken;
                            var withoutPrefix = token.RxNumberWithoutPrefix;
                            dbQuery = dbQuery.Where(p => Context.PrescriptionSearchTokens.Any(x =>
                                x.PrescriptionId == p.Id &&
                                x.TokenType == PrescriptionSearchTokenType.PrescriptionNumberPart &&
                                (x.Token == fullNorm || x.Token == withoutPrefix)));
                        }
                        else if (token.IsAmendment)
                        {
                            var norm = token.NormalizedToken;
                            var withHyphen = norm.StartsWith('-') ? norm : $"-{norm}";
                            var withoutHyphen = norm.StartsWith('-') ? norm[1..] : norm;
                            dbQuery = dbQuery.Where(p => Context.PrescriptionSearchTokens.Any(x =>
                                x.PrescriptionId == p.Id &&
                                x.TokenType == PrescriptionSearchTokenType.PrescriptionNumberPart &&
                                (x.Token == withHyphen || x.Token == withoutHyphen)));
                        }
                        else if (token.IsRecordNumberPrefix && token.RecordNumberDigits != null)
                        {
                            var recDigits = token.RecordNumberDigits;
                            var recNorm = token.NormalizedToken;
                            dbQuery = dbQuery.Where(p =>
                                Context.PrescriptionSearchTokens.Any(x => x.PrescriptionId == p.Id && (x.Token == recDigits || x.Token == recNorm)) ||
                                Context.PatientSearchTokens.Any(x => x.PatientId == p.PatientId && x.TokenType == SearchTokenType.RecordNumber && (x.Token == recDigits || x.Token == recNorm)));
                        }
                        else if (token.IsDigitsOnly)
                        {
                            var digits = token.StrippedDigits;
                            var norm = token.NormalizedToken;
                            if (digits.Length >= 3)
                            {
                                var prefix = $"{digits}%";
                                dbQuery = dbQuery.Where(p =>
                                    Context.PrescriptionSearchTokens.Any(x => x.PrescriptionId == p.Id && (x.Token == digits || x.Token == norm)) ||
                                    Context.PatientSearchTokens.Any(x => x.PatientId == p.PatientId &&
                                        ((x.TokenType == SearchTokenType.PhoneSuffix && EF.Functions.Like(x.Token, prefix)) ||
                                         (x.TokenType == SearchTokenType.RecordNumber && x.Token == digits))));
                            }
                            else
                            {
                                dbQuery = dbQuery.Where(p =>
                                    Context.PrescriptionSearchTokens.Any(x => x.PrescriptionId == p.Id && (x.Token == digits || x.Token == norm)) ||
                                    Context.PatientSearchTokens.Any(x => x.PatientId == p.PatientId && x.TokenType == SearchTokenType.RecordNumber && x.Token == digits));
                            }
                        }
                        else
                        {
                            var prefix = $"{token.NormalizedToken}%";
                            var norm = token.NormalizedToken;
                            dbQuery = dbQuery.Where(p =>
                                Context.PrescriptionSearchTokens.Any(x => x.PrescriptionId == p.Id && (x.Token == norm || EF.Functions.Like(x.Token, prefix))) ||
                                Context.PatientSearchTokens.Any(x => x.PatientId == p.PatientId &&
                                    ((x.TokenType == SearchTokenType.NameWord && EF.Functions.Like(x.Token, prefix)) ||
                                     (x.TokenType == SearchTokenType.RecordNumber && x.Token == norm))));
                        }
                    }
                }
                else
                {
                    // Fallback slower query while indexing is incomplete
                    foreach (var token in parsedTokens)
                    {
                        var term = $"%{token.NormalizedToken}%";
                        var digits = token.IsDigitsOnly ? token.StrippedDigits : null;

                        dbQuery = dbQuery.Where(p =>
                            EF.Functions.Like(p.PrescriptionNumber, term) ||
                            EF.Functions.Like(p.PatientSnapshot.Name, term) ||
                            p.Items.Any(i => EF.Functions.Like(i.MedicineName, term) || (i.GenericName != null && EF.Functions.Like(i.GenericName, term))) ||
                            (p.Patient != null && EF.Functions.Like(p.Patient.Name, term)) ||
                            (p.Patient != null && EF.Functions.Like(p.Patient.RecordNumber, term)) ||
                            (digits != null && p.Patient != null && p.Patient.PhoneDigits != null && EF.Functions.Like(p.Patient.PhoneDigits, $"%{digits}%")));
                    }
                }
            }
        }

        return dbQuery;
    }
}

