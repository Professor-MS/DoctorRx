using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class MedicineDraftPurgeRestrictionTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _connectionString;
    private readonly TestDbContextFactory _factory;

    public MedicineDraftPurgeRestrictionTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"DoctorRx_DraftPurge_{Guid.NewGuid():N}.db");
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _tempDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        _connectionString = builder.ConnectionString;

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;
        _factory = new TestDbContextFactory(options);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath))
            {
                SqliteConnection.ClearAllPools();
                File.Delete(_tempDbPath);
            }
        }
        catch { }
    }

    [Fact]
    public async Task PurgeMedicineAsync_WhenReferencedBySavedDraft_RejectsWithReferenceRestriction()
    {
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            await ctx.Database.MigrateAsync();
        }

        var clock = new SystemClock();
        var uowFactory = new UnitOfWorkFactory(_factory);
        var searchService = new MedicineSearchService(uowFactory, NullLogger<MedicineSearchService>.Instance);
        var medicineService = new MedicineService(uowFactory, clock, NullLogger<MedicineService>.Instance, searchService);
        var draftService = new DraftService(uowFactory, clock, NullLogger<DraftService>.Instance);

        // 1. Create a medicine
        var medResult = await medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "DraftRefMed",
            Form = "Tablet",
            Strength = "100 mg"
        });
        Assert.True(medResult.IsSuccess);
        var medId = medResult.Value!.Id;

        // Verify summary before draft: 0 references
        var initialSummary = await medicineService.GetMedicineUsageSummaryAsync(medId);
        Assert.False(initialSummary.IsReferencedInPrescriptions);
        Assert.False(initialSummary.IsReferencedInDrafts);

        // 2. Save a prescription draft referencing this medicine
        var draftKey = Guid.NewGuid();
        var draftState = new PrescriptionComposerState
        {
            DraftKey = draftKey,
            Items = new List<PrescriptionMedicineRowState>
            {
                new()
                {
                    MedicineId = medId,
                    MedicineName = "DraftRefMed",
                    Form = "Tablet",
                    Strength = "100 mg",
                    Dose = "1 tab",
                    Frequency = "BD"
                }
            }
        };

        var saveDraftResult = await draftService.SaveAsync(draftState);
        Assert.True(saveDraftResult.IsSuccess);

        // Verify summary after draft: IsReferencedInDrafts == true
        var updatedSummary = await medicineService.GetMedicineUsageSummaryAsync(medId);
        Assert.True(updatedSummary.IsReferencedInDrafts);
        Assert.Equal(1, updatedSummary.DraftReferenceCount);

        // 3. Act: Attempt to hard-delete (purge) the medicine
        var purgeResult = await medicineService.PurgeMedicineAsync(medId);

        // Assert: Must reject with typed ErrorCode ReferenceRestriction
        Assert.False(purgeResult.IsSuccess);
        Assert.Equal(ResultErrorCode.ReferenceRestriction, purgeResult.ErrorCode);
        Assert.Contains("saved prescription draft", purgeResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // 4. Discard the draft and verify purge now succeeds
        await draftService.DiscardAsync(draftKey);

        var purgeAfterDiscard = await medicineService.PurgeMedicineAsync(medId);
        Assert.True(purgeAfterDiscard.IsSuccess);
    }
}
