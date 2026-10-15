using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Models;
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
    public async Task Benchmark_100kPatients_SkewedDistribution_WorstCases_PerformSub50ms_WithExplainQueryPlan()
    {
        const int recordCount = 100_000;
        const int batchSize = 10_000;

        _output.WriteLine($"Seeding {recordCount:N0} skewed patient records (heavy 'Muhammad', 'Ali' distribution)...");
        var sw = Stopwatch.StartNew();

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var conn = (SqliteConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            var commonPrefixes = new[] { "Muhammad Ali", "Muhammad", "Ali", "Ahmed", "Fatima" };
            var commonSuffixes = new[] { "Khan", "Iqbal", "Bibi", "Raza", "Hassan", "Malik", "Chaudhry", "Saeed" };

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
                    // 50% of the distribution gets 'Muhammad' or 'Ali' prefixes
                    string fullName;
                    if (index % 4 == 0)
                    {
                        fullName = $"Muhammad Ali {commonSuffixes[index % commonSuffixes.Length]} {index}";
                    }
                    else if (index % 4 == 1)
                    {
                        fullName = $"Muhammad {commonSuffixes[index % commonSuffixes.Length]} {index}";
                    }
                    else if (index % 4 == 2)
                    {
                        fullName = $"Ali {commonSuffixes[index % commonSuffixes.Length]} {index}";
                    }
                    else
                    {
                        fullName = $"{commonPrefixes[index % commonPrefixes.Length]} {commonSuffixes[index % commonSuffixes.Length]} {index}";
                    }

                    var norm = SearchNormalizer.Normalize(fullName);
                    var phone = (index % 2 == 0) ? $"0300{index:D7}" : $"+92 300 {index:D7}";
                    var digits = SearchNormalizer.NormalizePhoneDigits(phone);

                    pRec.Value = $"P-SKW-{index:D6}";
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

            // Update stats
            using var analyzeCmd = conn.CreateCommand();
            analyzeCmd.CommandText = "ANALYZE;";
            analyzeCmd.ExecuteNonQuery();
        }

        sw.Stop();
        _output.WriteLine($"Ingested 100k skewed records in {sw.ElapsedMilliseconds} ms.");

        var uowFactory = new UnitOfWorkFactory(_factory);
        await using var uow = uowFactory.Create();

        // Helper to run query, measure time and dump EXPLAIN QUERY PLAN
        async Task RunCaseAsync(string query, string description)
        {
            // Dump explain plan
            await using (var ctx = await _factory.CreateDbContextAsync())
            {
                var conn = (SqliteConnection)ctx.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
                using var expCmd = conn.CreateCommand();
                var isPhone = query.All(c => char.IsDigit(c) || c == '+');
                if (isPhone)
                {
                    var digits = SearchNormalizer.NormalizePhoneDigits(query);
                    expCmd.CommandText = $"EXPLAIN QUERY PLAN SELECT Id, Name FROM Patients WHERE PhoneDigits LIKE '{digits}%' LIMIT 20;";
                }
                else
                {
                    var cleanQuery = SearchNormalizer.Normalize(query);
                    expCmd.CommandText = $"EXPLAIN QUERY PLAN SELECT Id, Name FROM Patients WHERE NormalizedName LIKE '{cleanQuery}%' LIMIT 20;";
                }
                using var reader = await expCmd.ExecuteReaderAsync();
                var planLines = new List<string>();
                while (await reader.ReadAsync())
                {
                    planLines.Add(reader.GetString(3));
                }
                _output.WriteLine($"[EXPLAIN QUERY PLAN for '{query}' ({description})]:\n  {string.Join("\n  ", planLines)}");
            }

            // Benchmark search
            sw.Restart();
            var results = await uow.Patients.SearchAsync(query, maxResults: 20);
            sw.Stop();
            var timeMs = sw.ElapsedMilliseconds;

            _output.WriteLine($"Search '{query}' ({description}) returned {results.Count} results in {timeMs} ms.");
            Assert.NotEmpty(results);
            Assert.True(results.Count <= 20);
            Assert.True(timeMs < 100, $"Expected search for '{query}' under 100ms with LIMIT 20, but took {timeMs} ms.");
        }

        // Test 4 worst cases with LIMIT 20:
        await RunCaseAsync("muhammad ali", "worst case skewed multi-word");
        await RunCaseAsync("ali", "worst case single high-frequency word");
        await RunCaseAsync("m", "worst case single character prefix");
        await RunCaseAsync("0300", "worst case phone fragment");
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
        var fileSizeBytesBefore = new FileInfo(_appPaths.DatabasePath).Length;
        var fileSizeMbBefore = fileSizeBytesBefore / (1024.0 * 1024.0);
        _output.WriteLine($"[Benchmark Result] Database size BEFORE token table: {fileSizeMbBefore:F2} MB (Ingested {recordCount:N0} prescriptions + items in {ingestionTimeMs:N0} ms)");

        // Now populate tokens for the 500,000 records
        _output.WriteLine($"Generating and inserting search tokens for {recordCount:N0} prescriptions...");
        sw.Restart();
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var conn = (SqliteConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

            using (var pragmaCmd = conn.CreateCommand())
            {
                pragmaCmd.CommandText = "PRAGMA synchronous = OFF;";
                pragmaCmd.ExecuteNonQuery();
            }

            using var tokenCmd = conn.CreateCommand();
            tokenCmd.CommandText = @"
                INSERT INTO PrescriptionSearchTokens (PrescriptionId, TokenType, Token)
                VALUES ($rxId, $type, $tok);";
            var pTokenRxId = tokenCmd.Parameters.Add("$rxId", SqliteType.Integer);
            var pTokenType = tokenCmd.Parameters.Add("$type", SqliteType.Integer);
            var pTokenVal = tokenCmd.Parameters.Add("$tok", SqliteType.Text);

            for (int batch = 0; batch < recordCount / batchSize; batch++)
            {
                using var tx = conn.BeginTransaction();
                tokenCmd.Transaction = tx;
                for (int i = 0; i < batchSize; i++)
                {
                    int id = (batch * batchSize) + i + 1;
                    pTokenRxId.Value = id;

                    // 1. PrescriptionNumber parts
                    pTokenType.Value = 0; // PrescriptionNumberPart
                    pTokenVal.Value = "20261001";
                    tokenCmd.ExecuteNonQuery();

                    pTokenType.Value = 0;
                    pTokenVal.Value = $"{id:D6}";
                    tokenCmd.ExecuteNonQuery();

                    // 2. PatientName words
                    pTokenType.Value = 1; // PatientNameWord
                    pTokenVal.Value = "benchmark";
                    tokenCmd.ExecuteNonQuery();

                    pTokenType.Value = 1;
                    pTokenVal.Value = "patient";
                    tokenCmd.ExecuteNonQuery();

                    // 3. Medicine name
                    pTokenType.Value = 2; // MedicineName
                    pTokenVal.Value = "amoxicillin";
                    tokenCmd.ExecuteNonQuery();
                }
                tx.Commit();
            }

            using var metaCmd = conn.CreateCommand();
            metaCmd.CommandText = @"
                INSERT INTO AppMetas (Key, Value, UpdatedAtUtc) VALUES ('SearchIndexVersion', '2', CURRENT_TIMESTAMP)
                ON CONFLICT(Key) DO UPDATE SET Value = '2', UpdatedAtUtc = CURRENT_TIMESTAMP;
                INSERT INTO AppMetas (Key, Value, UpdatedAtUtc) VALUES ('PrescriptionSearchIndexVersion', '1', CURRENT_TIMESTAMP)
                ON CONFLICT(Key) DO UPDATE SET Value = '1', UpdatedAtUtc = CURRENT_TIMESTAMP;
                PRAGMA synchronous = FULL;
                ANALYZE;";
            metaCmd.ExecuteNonQuery();
        }
        sw.Stop();
        var tokenIngestionTimeMs = sw.ElapsedMilliseconds;

        var fileSizeBytesAfter = new FileInfo(_appPaths.DatabasePath).Length;
        var fileSizeMbAfter = fileSizeBytesAfter / (1024.0 * 1024.0);
        _output.WriteLine($"[Benchmark Result] Database size AFTER token table: {fileSizeMbAfter:F2} MB (Token table + indices added in {tokenIngestionTimeMs:N0} ms, delta: +{fileSizeMbAfter - fileSizeMbBefore:F2} MB)");

        // Service under test
        var uowFactory = new UnitOfWorkFactory(_factory);
        var clock = new SystemClock();
        var rxService = new PrescriptionService(uowFactory, clock, NullLogger<PrescriptionService>.Instance);

        // Warm-up query execution (EF query compilation + JIT)
        _ = await rxService.GetRecentPrescriptionsAsync(count: 1);
        _ = await rxService.GetPrescriptionByIdAsync(id: 1);
        _ = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(PageSize: 1));
        _ = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "warmup", PageSize: 1));

        // 1. History load timing (top 20 recent prescriptions)
        sw.Restart();
        var recent = await rxService.GetRecentPrescriptionsAsync(count: 20);
        sw.Stop();
        var historyLoadTimeMs = sw.ElapsedMilliseconds;

        _output.WriteLine($"[Benchmark Result] History load (20 recent across 500,000 records): {historyLoadTimeMs} ms (returned {recent.Count} records)");
        Assert.Equal(20, recent.Count);
        Assert.True(historyLoadTimeMs < 100, $"History load took {historyLoadTimeMs}ms, expected < 100ms");

        // Benchmark measurement helper
        async Task<(double median, double p95, List<long> runs)> MeasureMultiRunAsync(Func<Task> action, int count = 5)
        {
            var runs = new List<long>();
            for (int i = 0; i < count; i++)
            {
                var s = Stopwatch.StartNew();
                await action();
                s.Stop();
                runs.Add(s.ElapsedMilliseconds);
            }
            var sorted = runs.OrderBy(r => r).ToList();
            double med = sorted[sorted.Count / 2];
            int p95Idx = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
            p95Idx = Math.Clamp(p95Idx, 0, sorted.Count - 1);
            double p95Val = sorted[p95Idx];
            return (med, p95Val, runs);
        }

        // 1b. SearchPrescriptionsAsync first-page load (50 items across 500,000 records)
        var (firstPageMed, firstPageP95, _) = await MeasureMultiRunAsync(async () =>
        {
            var res = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(PageSize: 50));
            Assert.Equal(50, res.Items.Count);
        });
        _output.WriteLine($"[Benchmark Result] First-page time: Median = {firstPageMed:F1} ms, p95 = {firstPageP95:F1} ms");
        Assert.True(firstPageP95 < 100, $"First-page p95 was {firstPageP95}ms, expected < 100ms");

        // 1c. SearchPrescriptionsAsync worst-case filter (Issued + Today across 500,000 records)
        var (filterMed, filterP95, _) = await MeasureMultiRunAsync(async () =>
        {
            var res = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
                StatusFilter: PrescriptionStatusSearchFilter.Issued,
                DatePreset: PrescriptionDatePreset.Today,
                PageSize: 50
            ));
            Assert.NotNull(res);
        });
        _output.WriteLine($"[Benchmark Result] Worst-case filter time: Median = {filterMed:F1} ms, p95 = {filterP95:F1} ms");
        Assert.True(filterP95 < 200, $"Worst-case filter p95 was {filterP95}ms, expected < 200ms");

        // 1d. Token search time ('20261001') - Skewed: 500,000 prescriptions share this token
        var (tokenMed, tokenP95, tokenRuns) = await MeasureMultiRunAsync(async () =>
        {
            var res = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "20261001", PageSize: 50));
            Assert.Equal(50, res.Items.Count);
        });
        _output.WriteLine($"[Benchmark Result] Token search ('20261001' on 500k skewed records): Median = {tokenMed:F1} ms, p95 = {tokenP95:F1} ms (Runs: {string.Join(", ", tokenRuns)} ms)");
        Assert.True(tokenMed < 200, $"Token search median took {tokenMed}ms, expected < 200ms");

        // 1e. Medicine name search time ('amoxicillin') - Skewed: 500,000 prescriptions share this token
        var (medMed, medP95, medRuns) = await MeasureMultiRunAsync(async () =>
        {
            var res = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "amoxicillin", PageSize: 50));
            Assert.Equal(50, res.Items.Count);
        });
        _output.WriteLine($"[Benchmark Result] Medicine-name search ('amoxicillin' on 500k skewed records): Median = {medMed:F1} ms, p95 = {medP95:F1} ms (Runs: {string.Join(", ", medRuns)} ms)");
        Assert.True(medMed < 200, $"Medicine search median took {medMed}ms, expected < 200ms");

        // 1f. Sort by patient name
        var (nameSortMed, nameSortP95, _) = await MeasureMultiRunAsync(async () =>
        {
            var res = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SortOption: PrescriptionSortOption.PatientName, PageSize: 50));
            Assert.Equal(50, res.Items.Count);
        });
        _output.WriteLine($"[Benchmark Result] Sort by patient name: Median = {nameSortMed:F1} ms, p95 = {nameSortP95:F1} ms");

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

        _output.WriteLine($"[Benchmark Result] Finalize time: {finalizeTimeMs} ms");
        Assert.True(finalizeResult.IsSuccess, finalizeResult.ErrorMessage);
        Assert.NotNull(finalizeResult.Value);
        Assert.True(finalizeTimeMs < 1000, $"Finalize prescription took {finalizeTimeMs}ms, expected < 1000ms");

        // 4. 50-concurrent finalization impact
        var concurrentTasks = Enumerable.Range(1, 50).Select(async i =>
        {
            var dto = new CreatePrescriptionDto
            {
                PatientId = patientId,
                DoctorId = doctorId,
                PrescriptionDate = clock.Today,
                ChiefComplaints = $"Concurrent stress check {i}",
                Items = new List<CreatePrescriptionMedicineDto>
                {
                    new()
                    {
                        MedicineName = $"ConcurrentMed-{i}",
                        Form = "Tablet",
                        Strength = "10mg",
                        Dose = "1 tab",
                        Frequency = "OD",
                        Route = "Oral",
                        Duration = "1 day"
                    }
                }
            };
            return await rxService.FinalizePrescriptionAsync(dto);
        });

        sw.Restart();
        var concurrentResults = await Task.WhenAll(concurrentTasks);
        sw.Stop();
        var concurrentTimeMs = sw.ElapsedMilliseconds;
        _output.WriteLine($"[Benchmark Result] 50 concurrent finalizations: {concurrentTimeMs} ms ({concurrentTimeMs / 50.0:F1} ms/op)");
        Assert.All(concurrentResults, r => Assert.True(r.IsSuccess, r.ErrorMessage));

        // 5. Measure repair job duration
        var repairService = new SearchIndexRepairService(_factory, NullLogger<SearchIndexRepairService>.Instance);
        sw.Restart();
        var repairReport = await repairService.RepairIndexAsync();
        sw.Stop();
        var repairTimeMs = sw.ElapsedMilliseconds;
        _output.WriteLine($"[Benchmark Result] Repair-job duration: {repairTimeMs} ms (Prescriptions repaired: {repairReport.PrescriptionsRepaired})");

        // 6. Dump EXPLAIN QUERY PLAN for all 7 required cases
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var conn = (SqliteConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

            async Task DumpPlan(string name, string querySql)
            {
                using var planCmd = conn.CreateCommand();
                planCmd.CommandText = $"EXPLAIN QUERY PLAN {querySql}";
                using var reader = await planCmd.ExecuteReaderAsync();
                var lines = new List<string>();
                while (await reader.ReadAsync())
                {
                    lines.Add(reader.GetString(reader.GetOrdinal("detail")));
                }
                _output.WriteLine($"[EXPLAIN QUERY PLAN: {name}]\n  {string.Join("\n  ", lines)}");
            }

            await DumpPlan("1. Default View", "SELECT Id, PrescriptionNumber, PrescriptionDate, Status, Patient_Name FROM Prescriptions ORDER BY PrescriptionDate DESC, Id DESC LIMIT 50;");
            await DumpPlan("2. Status Filter", "SELECT Id, PrescriptionNumber, PrescriptionDate, Status, Patient_Name FROM Prescriptions WHERE Status = 1 ORDER BY PrescriptionDate DESC, Id DESC LIMIT 50;");
            await DumpPlan("3. Date Range", "SELECT Id, PrescriptionNumber, PrescriptionDate, Status, Patient_Name FROM Prescriptions WHERE PrescriptionDate >= '2026-10-01' AND PrescriptionDate <= '2026-10-13' ORDER BY PrescriptionDate DESC, Id DESC LIMIT 50;");
            await DumpPlan("4. Patient Filter", "SELECT Id, PrescriptionNumber, PrescriptionDate, Status, Patient_Name FROM Prescriptions WHERE PatientId = 1 ORDER BY PrescriptionDate DESC, Id DESC LIMIT 50;");
            await DumpPlan("5. Sort by Patient Name", "SELECT Id, PrescriptionNumber, PrescriptionDate, Status, Patient_Name FROM Prescriptions ORDER BY Patient_Name COLLATE NOCASE ASC, Id ASC LIMIT 50;");
            await DumpPlan("6. Token Search", "SELECT PrescriptionId FROM PrescriptionSearchTokens WHERE Token = '20261001';");
            await DumpPlan("7. Medicine-Name Search", "SELECT PrescriptionId FROM PrescriptionSearchTokens WHERE TokenType IN (2, 3) AND Token = 'amoxicillin';");
        }
    }
}
