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

        await using (var context = await _contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var meta = await context.AppMetas.FirstOrDefaultAsync(m => m.Key == "SearchIndexVersion", cancellationToken);
            if (meta?.Value == CurrentIndexVersion)
            {
                // Quick check: verify if there are any orphan or unindexed patients
                var anyUnindexed = await context.Patients.AnyAsync(p => p.NormalizedName == "" || (p.SearchTokens.Count == 0 && p.Name != ""), cancellationToken);
                if (!anyUnindexed)
                {
                    _logger.LogInformation("Search index version {Version} is already current and healthy.", CurrentIndexVersion);
                    return new SearchIndexRepairReport(0, 0, 0, 0);
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
                    // Check if any expected token is missing
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
                await context.SaveChangesAsync(cancellationToken);
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
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        // Update version metadata
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

            await context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Search index repair completed without PII: {CheckedPatients} patients checked ({RepairedPatients} repaired); {CheckedMeds} medicines checked ({RepairedMeds} repaired).",
            patientsChecked, patientsRepaired, medicinesChecked, medicinesRepaired);

        return new SearchIndexRepairReport(patientsChecked, patientsRepaired, medicinesChecked, medicinesRepaired);
    }
}
