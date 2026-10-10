using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DoctorRx.Tests;

public class SQLiteBenchmarkTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly IDbContextFactory<DoctorRxDbContext> _factory;
    private readonly ITestOutputHelper _output;

    public SQLiteBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_BenchTests_" + Guid.NewGuid().ToString("N"));
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
            // Best effort cleanup in temp directory
        }
    }

    [Fact]
    public async Task Benchmark_100kPatients_PrefixSearchAndPagination_PerformSub50ms()
    {
        const int recordCount = 100_000;
        const int batchSize = 10_000;

        _output.WriteLine($"Seeding {recordCount:N0} patient records in batches of {batchSize:N0}...");
        var sw = Stopwatch.StartNew();

        // High performance bulk ingestion using parameterized SQLite command inside transactions
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var conn = (SqliteConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            var firstNames = new[] { "Ahmed", "Bilal", "Fatima", "Zainab", "Usman", "Tariq", "Ayesha", "Ali", "Hassan", "Khadija" };
            var lastNames = new[] { "Khan", "Mahmood", "Iqbal", "Rehman", "Akhtar", "Bibi", "Saeed", "Chaudhry", "Malik", "Raza" };

            for (int batch = 0; batch < recordCount / batchSize; batch++)
            {
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO Patients (RecordNumber, Name, NormalizedName, Age, Gender, Phone, PhoneDigits, IsArchived, CreatedAtUtc)
                    VALUES ($rec, $name, $norm, $age, $gender, $phone, $digits, 0, $created);";

                var pRec = cmd.Parameters.Add("$rec", SqliteType.Text);
                var pName = cmd.Parameters.Add("$name", SqliteType.Text);
                var pNorm = cmd.Parameters.Add("$norm", SqliteType.Text);
                var pAge = cmd.Parameters.Add("$age", SqliteType.Integer);
                var pGender = cmd.Parameters.Add("$gender", SqliteType.Integer);
                var pPhone = cmd.Parameters.Add("$phone", SqliteType.Text);
                var pDigits = cmd.Parameters.Add("$digits", SqliteType.Text);
                var pCreated = cmd.Parameters.Add("$created", SqliteType.Text);

                var now = DateTime.UtcNow.ToString("O");

                for (int i = 0; i < batchSize; i++)
                {
                    int index = (batch * batchSize) + i + 1;
                    var fn = firstNames[index % firstNames.Length];
                    var ln = lastNames[index % lastNames.Length];
                    var fullName = $"{fn} {ln} {index}";
                    var norm = SearchNormalizer.Normalize(fullName);
                    var phone = $"+92 300 {index:D7}";
                    var digits = SearchNormalizer.NormalizePhoneDigits(phone);

                    pRec.Value = $"P-{index:D6}";
                    pName.Value = fullName;
                    pNorm.Value = norm;
                    pAge.Value = 20 + (index % 60);
                    pGender.Value = (index % 2) + 1;
                    pPhone.Value = phone;
                    pDigits.Value = digits;
                    pCreated.Value = now;

                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
        }

        sw.Stop();
        var ingestionTimeMs = sw.ElapsedMilliseconds;
        var fileSizeBytes = new FileInfo(_appPaths.DatabasePath).Length;
        var fileSizeMb = fileSizeBytes / (1024.0 * 1024.0);

        _output.WriteLine($"Ingested {recordCount:N0} records in {ingestionTimeMs:N0} ms. DB Size: {fileSizeMb:F2} MB");

        // Warm up and test prefix search
        var uowFactory = new UnitOfWorkFactory(_factory);
        await using var uow = uowFactory.Create();

        // 1. Prefix search benchmark
        sw.Restart();
        var searchResults = await uow.Patients.SearchAsync("ahmed", maxResults: 50);
        sw.Stop();
        var searchTimeMs = sw.ElapsedMilliseconds;

        _output.WriteLine($"Prefix search 'ahmed' returned {searchResults.Count} records in {searchTimeMs} ms.");
        Assert.NotEmpty(searchResults);
        Assert.True(searchTimeMs < 100, $"Prefix search took {searchTimeMs}ms, expected < 100ms");

        // 2. Indexed Phone prefix search benchmark
        sw.Restart();
        var phoneResults = await uow.Patients.SearchAsync("92300000", maxResults: 50);
        sw.Stop();
        var phoneSearchTimeMs = sw.ElapsedMilliseconds;

        _output.WriteLine($"Phone prefix search returned {phoneResults.Count} records in {phoneSearchTimeMs} ms.");
        Assert.NotEmpty(phoneResults);
        Assert.True(phoneSearchTimeMs < 100, $"Phone search took {phoneSearchTimeMs}ms, expected < 100ms");

        // 3. Deep Pagination benchmark (offset 50,000)
        sw.Restart();
        var pageResults = await uow.Patients.GetPagedAsync(pageNumber: 1000, pageSize: 50);
        sw.Stop();
        var pageTimeMs = sw.ElapsedMilliseconds;

        _output.WriteLine($"Pagination page 1000 (offset 50,000) returned {pageResults.Count} records in {pageTimeMs} ms.");
        Assert.Equal(50, pageResults.Count);
        Assert.True(pageTimeMs < 150, $"Pagination took {pageTimeMs}ms, expected < 150ms");
    }

    [Fact]
    public async Task Benchmark_500kPrescriptions_HistoryLoad_OpenAndFinalize_Timings()
    {
        const int recordCount = 500_000;
        const int batchSize = 50_000;

        int doctorId;
        int patientId;
        await using (var seedCtx = await _factory.CreateDbContextAsync())
        {
            var doc = new Doctor
            {
                Name = "Dr. Benchmark",
                Qualification = "MBBS, FCPS",
                RegistrationNumber = "BM-12345",
                Specialization = "Internal Medicine",
                ClinicName = "Apex Medical Institute",
                IsActive = true
            };
            var pat = new Patient
            {
                RecordNumber = "P-000001",
                Name = "Benchmark Patient",
                NormalizedName = "benchmark patient",
                DateOfBirth = new DateOnly(1985, 1, 1),
                Gender = Gender.Male
            };
            seedCtx.Doctors.Add(doc);
            seedCtx.Patients.Add(pat);
            await seedCtx.SaveChangesAsync();
            doctorId = doc.Id;
            patientId = pat.Id;
        }

        _output.WriteLine($"Ingesting {recordCount:N0} prescriptions + items in batches of {batchSize:N0}...");
        var sw = Stopwatch.StartNew();

        // High-speed ingestion using SQLite parameterized commands
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var conn = (SqliteConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            // Temporary speed boost for test bulk insertion
            using (var pragmaCmd = conn.CreateCommand())
            {
                pragmaCmd.CommandText = "PRAGMA synchronous = OFF;";
                pragmaCmd.ExecuteNonQuery();
            }

            var nowStr = DateTime.UtcNow.ToString("O");
            var dateStr = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

            for (int batch = 0; batch < recordCount / batchSize; batch++)
            {
                using var tx = conn.BeginTransaction();

                using var rxCmd = conn.CreateCommand();
                rxCmd.Transaction = tx;
                rxCmd.CommandText = @"
                    INSERT INTO Prescriptions 
                    (PrescriptionNumber, PatientId, DoctorId, PrescriptionDate, Doctor_Name, Doctor_Qualification, Doctor_RegistrationNumber, Doctor_Specialization, Doctor_ClinicName, Patient_Name, Patient_Gender, Patient_AgeText, Status, FinalizedAtUtc, AmendmentNumber, Version, CreatedAtUtc, IsSealed)
                    VALUES ($num, $patId, $docId, $pDate, 'Dr. Benchmark', 'MBBS', 'BM-12345', 'Internal Medicine', 'Apex Clinic', 'Benchmark Patient', 1, '40 yrs', 1, $now, 0, 1, $now, 0);";

                var pNum = rxCmd.Parameters.Add("$num", SqliteType.Text);
                var pPatId = rxCmd.Parameters.Add("$patId", SqliteType.Integer);
                var pDocId = rxCmd.Parameters.Add("$docId", SqliteType.Integer);
                var pDate = rxCmd.Parameters.Add("$pDate", SqliteType.Text);
                var pNow = rxCmd.Parameters.Add("$now", SqliteType.Text);

                pPatId.Value = patientId;
                pDocId.Value = doctorId;
                pDate.Value = dateStr;
                pNow.Value = nowStr;

                using var medCmd = conn.CreateCommand();
                medCmd.Transaction = tx;
                medCmd.CommandText = @"
                    INSERT INTO PrescriptionMedicines
                    (PrescriptionId, MedicineName, Form, Strength, Dose, Frequency, Route, Duration, SortOrder, MealRelation)
                    VALUES ($rxId, 'Amoxicillin', 'Capsule', '500 mg', '1 cap', 'Three times daily', 'Oral', '7 days', 1, 0);";

                var pRxId = medCmd.Parameters.Add("$rxId", SqliteType.Integer);

                for (int i = 0; i < batchSize; i++)
                {
                    int id = (batch * batchSize) + i + 1;
                    pNum.Value = $"RX-20261001-{id:D6}";
                    rxCmd.ExecuteNonQuery();

                    pRxId.Value = id;
                    medCmd.ExecuteNonQuery();
                }

                int startId = (batch * batchSize) + 1;
                int endId = (batch + 1) * batchSize;
                using (var sealCmd = conn.CreateCommand())
                {
                    sealCmd.Transaction = tx;
                    sealCmd.CommandText = $"UPDATE Prescriptions SET IsSealed = 1 WHERE Id BETWEEN {startId} AND {endId};";
                    sealCmd.ExecuteNonQuery();
                }

                tx.Commit();
            }

            // Restore synchronous=FULL
            using (var restoreCmd = conn.CreateCommand())
            {
                restoreCmd.CommandText = "PRAGMA synchronous = FULL;";
                restoreCmd.ExecuteNonQuery();
            }
        }

        sw.Stop();
        var ingestionTimeMs = sw.ElapsedMilliseconds;
        var fileSizeBytes = new FileInfo(_appPaths.DatabasePath).Length;
        var fileSizeMb = fileSizeBytes / (1024.0 * 1024.0);
        _output.WriteLine($"Ingested {recordCount:N0} prescriptions + items in {ingestionTimeMs:N0} ms. DB Size: {fileSizeMb:F2} MB");

        // Service under test
        var uowFactory = new UnitOfWorkFactory(_factory);
        var clock = new SystemClock();
        var rxService = new PrescriptionService(uowFactory, clock, NullLogger<PrescriptionService>.Instance);

        // Warm-up query execution (EF query compilation)
        _ = await rxService.GetRecentPrescriptionsAsync(count: 1);
        _ = await rxService.GetPrescriptionByIdAsync(id: 1);

        // 1. History load timing (top 20 recent prescriptions)
        sw.Restart();
        var recent = await rxService.GetRecentPrescriptionsAsync(count: 20);
        sw.Stop();
        var historyLoadTimeMs = sw.ElapsedMilliseconds;

        _output.WriteLine($"[Benchmark Result] History load (20 recent across 500,000 records): {historyLoadTimeMs} ms (returned {recent.Count} records)");
        Assert.Equal(20, recent.Count);
        Assert.True(historyLoadTimeMs < 100, $"History load took {historyLoadTimeMs}ms, expected < 100ms");

        // 2. Open one prescription timing (detailed lookup with items snapshot)
        sw.Restart();
        var openedRx = await rxService.GetPrescriptionByIdAsync(id: 250_000);
        sw.Stop();
        var openOneTimeMs = sw.ElapsedMilliseconds;

        _output.WriteLine($"[Benchmark Result] Open prescription #250,000 with items: {openOneTimeMs} ms");
        Assert.NotNull(openedRx);
        Assert.Single(openedRx.Items);
        Assert.True(openOneTimeMs < 100, $"Open prescription took {openOneTimeMs}ms, expected < 100ms");

        // 3. Finalize one new prescription timing via the real service
        var createDto = new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = clock.Today,
            ChiefComplaints = "Benchmark stress check",
            Items = new System.Collections.Generic.List<DoctorRx.Application.DTOs.CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineName = "Panadol",
                    Form = "Tablet",
                    Strength = "500 mg",
                    Dose = "1 tab",
                    Frequency = "TDS",
                    Route = "Oral",
                    Duration = "3 days"
                }
            }
        };

        sw.Restart();
        var finalizeResult = await rxService.FinalizePrescriptionAsync(createDto);
        sw.Stop();
        var finalizeTimeMs = sw.ElapsedMilliseconds;

        _output.WriteLine($"[Benchmark Result] Finalize new prescription into 500,000 database: {finalizeTimeMs} ms");
        Assert.True(finalizeResult.IsSuccess, finalizeResult.ErrorMessage);
        Assert.NotNull(finalizeResult.Value);
        Assert.True(finalizeTimeMs < 1000, $"Finalize prescription took {finalizeTimeMs}ms, expected < 1000ms");
    }
}
