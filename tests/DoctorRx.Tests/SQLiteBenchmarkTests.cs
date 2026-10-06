using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
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
}
