using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Entities;
using DoctorRx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Infrastructure.Services;

public class SearchIndexRepairService : ISearchIndexRepairService
{
    private const string CurrentIndexVersion = "2";
    private const string CurrentPrescriptionIndexVersion = "1";
    private const int BatchSize = 100;

    private readonly IDbContextFactory<DoctorRxDbContext> _contextFactory;
    private readonly ILogger<SearchIndexRepairService> _logger;

    public SearchIndexRepairService(
        IDbContextFactory<DoctorRxDbContext> contextFactory,
        ILogger<SearchIndexRepairService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<SearchIndexRepairReport> RepairIndexAsync(CancellationToken cancellationToken = default)
    {
        int patientsChecked = 0;
        int patientsRepaired = 0;
        int medicinesChecked = 0;
        int medicinesRepaired = 0;
        int prescriptionsChecked = 0;
        int prescriptionsRepaired = 0;
        string? rxMetaVersion = null;

        await using (var context = await _contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var meta = await context.AppMetas.FirstOrDefaultAsync(m => m.Key == "SearchIndexVersion", cancellationToken);
            var rxMeta = await context.AppMetas.FirstOrDefaultAsync(m => m.Key == "PrescriptionSearchIndexVersion", cancellationToken);
            rxMetaVersion = rxMeta?.Value;

            if (meta?.Value == CurrentIndexVersion && rxMeta?.Value == CurrentPrescriptionIndexVersion)
            {
                var anyUnindexedPatients = await context.Patients.AnyAsync(p => p.NormalizedName == "" || (!p.SearchTokens.Any() && p.Name != ""), cancellationToken);
                var anyUnindexedPrescriptions = await context.Prescriptions.AnyAsync(p => !p.SearchTokens.Any() && p.PrescriptionNumber != "", cancellationToken);

                if (!anyUnindexedPatients && !anyUnindexedPrescriptions)
                {
                    _logger.LogInformation("Search indexes are already current and healthy.");
                    return new SearchIndexRepairReport(0, 0, 0, 0, 0, 0);
                }
            }
        }

        _logger.LogInformation("Starting search index verification and repair job...");

        // 1. Repair Patients in chunked transactions
        int lastPatientId = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var batch = await context.Patients
                .Include(p => p.SearchTokens)
                .Where(p => p.Id > lastPatientId)
                .OrderBy(p => p.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0) break;

            var batchRepaired = 0;
            foreach (var patient in batch)
            {
                patientsChecked++;
                lastPatientId = patient.Id;

                var expectedNormalized = SearchNormalizer.Normalize(patient.Name);
                var expectedPhoneDigits = SearchNormalizer.NormalizePhoneDigits(patient.Phone);
                var expectedTokens = SearchNormalizer.GeneratePatientTokens(patient);

                var needsRepair = patient.NormalizedName != expectedNormalized ||
                                  (patient.PhoneDigits ?? string.Empty) != expectedPhoneDigits ||
                                  patient.SearchTokens.Count != expectedTokens.Count;

                if (!needsRepair)
                {
                    var storedTokens = new HashSet<string>(patient.SearchTokens.Select(t => $"{t.TokenType}:{t.Token}"), StringComparer.OrdinalIgnoreCase);
                    var expectedSet = expectedTokens.Select(t => $"{t.TokenType}:{t.Token}");
                    if (!expectedSet.All(storedTokens.Contains))
                    {
                        needsRepair = true;
                    }
                }

                if (needsRepair)
                {
                    context.PatientSearchTokens.RemoveRange(patient.SearchTokens);
                    patient.RefreshSearchFields();
                    foreach (var token in patient.SearchTokens)
                    {
                        context.PatientSearchTokens.Add(token);
                    }
                    batchRepaired++;
                    patientsRepaired++;
                }
            }

            if (batchRepaired > 0)
            {
                await SaveWithBusyRetryAsync(context, cancellationToken);
            }
        }

        // 2. Repair Medicines in chunked transactions
        int lastMedicineId = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var batch = await context.Medicines
                .Include(m => m.SearchTokens)
                .Where(m => m.Id > lastMedicineId)
                .OrderBy(m => m.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0) break;

            var batchRepaired = 0;
            foreach (var medicine in batch)
            {
                medicinesChecked++;
                lastMedicineId = medicine.Id;

                var expectedNormalized = SearchNormalizer.Normalize(medicine.Name);
                var expectedTokens = SearchNormalizer.GenerateMedicineTokens(medicine);

                var needsRepair = medicine.NormalizedName != expectedNormalized ||
                                  medicine.SearchTokens.Count != expectedTokens.Count;

                if (!needsRepair)
                {
                    var storedTokens = new HashSet<string>(medicine.SearchTokens.Select(t => $"{t.TokenType}:{t.Token}"), StringComparer.OrdinalIgnoreCase);
                    var expectedSet = expectedTokens.Select(t => $"{t.TokenType}:{t.Token}");
                    if (!expectedSet.All(storedTokens.Contains))
                    {
                        needsRepair = true;
                    }
                }

                if (needsRepair)
                {
                    context.MedicineSearchTokens.RemoveRange(medicine.SearchTokens);
                    medicine.RefreshSearchFields();
                    foreach (var token in medicine.SearchTokens)
                    {
                        context.MedicineSearchTokens.Add(token);
                    }
                    batchRepaired++;
                    medicinesRepaired++;
                }
            }

            if (batchRepaired > 0)
            {
                await SaveWithBusyRetryAsync(context, cancellationToken);
            }
        }

        // 3. Repair Prescriptions in chunked transactions
        int lastPrescriptionId = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.Prescriptions
                .Include(p => p.SearchTokens)
                .Include(p => p.Items)
                .AsQueryable();

            if (rxMetaVersion == CurrentPrescriptionIndexVersion)
            {
                query = query.Where(p => !p.SearchTokens.Any());
            }

            var batch = await query
                .Where(p => p.Id > lastPrescriptionId)
                .OrderBy(p => p.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0) break;

            var batchRepaired = 0;
            foreach (var rx in batch)
            {
                prescriptionsChecked++;
                lastPrescriptionId = rx.Id;

                var expectedTokens = SearchNormalizer.GeneratePrescriptionTokens(rx);
                var needsRepair = rx.SearchTokens.Count != expectedTokens.Count;

                if (!needsRepair)
                {
                    var storedTokens = new HashSet<string>(rx.SearchTokens.Select(t => $"{(int)t.TokenType}:{t.Token}"), StringComparer.OrdinalIgnoreCase);
                    var expectedSet = expectedTokens.Select(t => $"{(int)t.TokenType}:{t.Token}");
                    if (!expectedSet.All(storedTokens.Contains))
                    {
                        needsRepair = true;
                    }
                }

                if (needsRepair)
                {
                    context.PrescriptionSearchTokens.RemoveRange(rx.SearchTokens);
                    foreach (var token in expectedTokens)
                    {
                        token.PrescriptionId = rx.Id;
                        context.PrescriptionSearchTokens.Add(token);
                    }
                    batchRepaired++;
                    prescriptionsRepaired++;
                }
            }

            if (batchRepaired > 0)
            {
                await SaveWithBusyRetryAsync(context, cancellationToken);
            }
        }

        // Update version metadata if not cancelled early
        if (!cancellationToken.IsCancellationRequested)
        {
            await using (var context = await _contextFactory.CreateDbContextAsync(cancellationToken))
            {
                var meta = await context.AppMetas.FirstOrDefaultAsync(m => m.Key == "SearchIndexVersion", cancellationToken);
                if (meta == null)
                {
                    context.AppMetas.Add(new AppMeta
                    {
                        Key = "SearchIndexVersion",
                        Value = CurrentIndexVersion,
                        UpdatedAtUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    meta.Value = CurrentIndexVersion;
                    meta.UpdatedAtUtc = DateTime.UtcNow;
                }

                var rxMeta = await context.AppMetas.FirstOrDefaultAsync(m => m.Key == "PrescriptionSearchIndexVersion", cancellationToken);
                if (rxMeta == null)
                {
                    context.AppMetas.Add(new AppMeta
                    {
                        Key = "PrescriptionSearchIndexVersion",
                        Value = CurrentPrescriptionIndexVersion,
                        UpdatedAtUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    rxMeta.Value = CurrentPrescriptionIndexVersion;
                    rxMeta.UpdatedAtUtc = DateTime.UtcNow;
                }

                await SaveWithBusyRetryAsync(context, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Search index repair completed without PII: {CheckedPatients} patients checked ({RepairedPatients} repaired); {CheckedMeds} medicines checked ({RepairedMeds} repaired); {CheckedRx} prescriptions checked ({RepairedRx} repaired).",
            patientsChecked, patientsRepaired, medicinesChecked, medicinesRepaired, prescriptionsChecked, prescriptionsRepaired);

        return new SearchIndexRepairReport(patientsChecked, patientsRepaired, medicinesChecked, medicinesRepaired, prescriptionsChecked, prescriptionsRepaired);
    }

    private static async Task SaveWithBusyRetryAsync(DoctorRxDbContext context, CancellationToken cancellationToken)
    {
        const int maxRetries = 5;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (IsSqliteBusy(ex) && retry < maxRetries - 1)
            {
                await Task.Delay(50 * (retry + 1), cancellationToken);
            }
        }
    }

    private static bool IsSqliteBusy(Exception ex)
    {
        return ex is Microsoft.Data.Sqlite.SqliteException sex &&
               (sex.SqliteErrorCode == 5 /* SQLITE_BUSY */ || sex.SqliteErrorCode == 6 /* SQLITE_LOCKED */);
    }
}

