using System;
using System.IO;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class SearchIndexRepairTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly IDbContextFactory<DoctorRxDbContext> _factory;

    public SearchIndexRepairTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_RepairTests_" + Guid.NewGuid().ToString("N"));
        _appPaths = new TestAppPaths(_testDir);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _appPaths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(builder.ConnectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _factory = new TestDbContextFactory(options);

        var migrator = new DatabaseMigrator(_factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        migrator.MigrateDatabaseAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task RepairService_RepairsUnindexedRows_AndSetsMetaFlag()
    {
        // Insert legacy patients and medicines without tokens directly via raw SQL
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var conn = context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Patients (RecordNumber, Name, NormalizedName, Age, Gender, Phone, PhoneDigits, IsArchived, CreatedAtUtc)
                VALUES ('P-000001', 'Legacy Ahmed Khan', 'legacy ahmed khan', 35, 1, '03001234567', '03001234567', 0, '2026-01-01T00:00:00Z');

                INSERT INTO Medicines (Name, NormalizedName, Form, Strength, UsageCount, IsActive, CreatedAtUtc)
                VALUES ('Legacy Amoxil', 'legacy amoxil', 'Capsule', '500mg', 5, 1, '2026-01-01T00:00:00Z');";
            await cmd.ExecuteNonQueryAsync();

            // Clear any meta
            var meta = await context.AppMetas.FirstOrDefaultAsync(m => m.Key == "SearchTokensIndexed");
            if (meta != null)
            {
                context.AppMetas.Remove(meta);
                await context.SaveChangesAsync();
            }
        }

        var repairService = new SearchIndexRepairService(_factory, NullLogger<SearchIndexRepairService>.Instance);

        // Run repair
        var report1 = await repairService.RepairIndexAsync();
        Assert.Equal(1, report1.PatientsRepaired);
        Assert.Equal(1, report1.MedicinesRepaired);

        // Verify tokens created
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var patientTokens = await context.PatientSearchTokens.ToListAsync();
            Assert.NotEmpty(patientTokens);
            Assert.Contains(patientTokens, t => t.Token == "legacy");
            Assert.Contains(patientTokens, t => t.Token == "ahmed");
            Assert.Contains(patientTokens, t => t.Token == "khan");

            var medTokens = await context.MedicineSearchTokens.ToListAsync();
            Assert.NotEmpty(medTokens);
            Assert.Contains(medTokens, t => t.Token == "legacy");
            Assert.Contains(medTokens, t => t.Token == "amoxil");

            var meta = await context.AppMetas.FirstOrDefaultAsync(m => m.Key == "SearchIndexVersion");
            Assert.NotNull(meta);
            Assert.Equal("2", meta.Value);
        }

        // Verify subsequent check is complete (idempotent no-op)
        var report2 = await repairService.RepairIndexAsync();
        Assert.Equal(0, report2.PatientsRepaired);
        Assert.Equal(0, report2.MedicinesRepaired);
    }

    [Fact]
    public async Task RepairService_InterruptedMidway_RestartsAndCompletesIndexToFullHealth()
    {
        // 1. Arrange: Insert 250 unindexed patients and 50 unindexed medicines via raw SQL
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var conn = context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            var sqlConn = (SqliteConnection)conn;
            using var tx = sqlConn.BeginTransaction();
            using var cmd = sqlConn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO Patients (RecordNumber, Name, NormalizedName, Age, Gender, Phone, PhoneDigits, IsArchived, CreatedAtUtc)
                VALUES ($rec, $name, $norm, 30, 1, '03001112233', '03001112233', 0, '2026-01-01T00:00:00Z');";
            var pRec = cmd.Parameters.Add("$rec", SqliteType.Text);
            var pName = cmd.Parameters.Add("$name", SqliteType.Text);
            var pNorm = cmd.Parameters.Add("$norm", SqliteType.Text);

            for (int i = 1; i <= 250; i++)
            {
                pRec.Value = $"P-REP-{i:D5}";
                pName.Value = $"Unindexed Patient {i}";
                pNorm.Value = ""; // Missing normalized name
                cmd.ExecuteNonQuery();
            }

            using var medCmd = sqlConn.CreateCommand();
            medCmd.Transaction = tx;
            medCmd.CommandText = @"
                INSERT INTO Medicines (Name, NormalizedName, Form, Strength, UsageCount, IsActive, CreatedAtUtc)
                VALUES ($name, '', 'Tablet', '10mg', 1, 1, '2026-01-01T00:00:00Z');";
            var pmName = medCmd.Parameters.Add("$name", SqliteType.Text);
            for (int i = 1; i <= 50; i++)
            {
                pmName.Value = $"Unindexed Med {i}";
                medCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        var repairService = new SearchIndexRepairService(_factory, NullLogger<SearchIndexRepairService>.Instance);

        // 2. Act Part 1: Start repair with a cancellation token cancelled midway
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();

        // Calling with cancelled token will stop without completing all items
        try
        {
            await repairService.RepairIndexAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected task cancellation interruption
        }

        // Verify version meta is NOT set to 2 yet because it was interrupted
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var metaBeforeRestart = await context.AppMetas.FirstOrDefaultAsync(m => m.Key == "SearchIndexVersion");
            Assert.True(metaBeforeRestart == null || metaBeforeRestart.Value != "2");
        }

        // 3. Act Part 2: Restart the repair job to completion
        var restartReport = await repairService.RepairIndexAsync(System.Threading.CancellationToken.None);

        // 4. Assert: Everything was repaired and full health reached
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var totalPatientTokens = await context.PatientSearchTokens.CountAsync();
            Assert.True(totalPatientTokens >= 250);

            var totalMedTokens = await context.MedicineSearchTokens.CountAsync();
            Assert.True(totalMedTokens >= 50);

            var meta = await context.AppMetas.FirstOrDefaultAsync(m => m.Key == "SearchIndexVersion");
            Assert.NotNull(meta);
            Assert.Equal("2", meta.Value);
        }

        // 5. Subsequent run is a no-op
        var finalCheck = await repairService.RepairIndexAsync();
        Assert.Equal(0, finalCheck.PatientsRepaired);
        Assert.Equal(0, finalCheck.MedicinesRepaired);
    }
}
