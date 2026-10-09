using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Entities;
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

public class DatabaseHealthTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly PhysicalFileSystem _fileSystem;
    private readonly TestDbContextFactory _factory;
    private readonly IClock _clock;
    private readonly SearchIndexRepairService _searchRepair;
    private readonly DatabaseHealthService _healthService;
    private readonly DatabaseMigrator _migrator;
    private readonly DemoDataSeeder _seeder;

    public DatabaseHealthTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_HealthTests_" + Guid.NewGuid().ToString("N"));
        _appPaths = new TestAppPaths(_testDir);
        _fileSystem = new PhysicalFileSystem();
        _clock = new SystemClock();

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
        _searchRepair = new SearchIndexRepairService(_factory, NullLogger<SearchIndexRepairService>.Instance);
        _healthService = new DatabaseHealthService(
            _factory,
            _appPaths,
            _fileSystem,
            _clock,
            _searchRepair,
            NullLogger<DatabaseHealthService>.Instance);
        _migrator = new DatabaseMigrator(_factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        _seeder = new DemoDataSeeder(_factory, NullLogger<DemoDataSeeder>.Instance);
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
            // Best-effort cleanup
        }
    }

    private async Task SeedCleanDatabaseAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();

        var doctor = new Doctor
        {
            Name = "Dr. Health Test",
            Qualification = "MBBS",
            RegistrationNumber = "PMC-4444",
            Specialization = "General Physician",
            ClinicName = "Clinic A",
            IsActive = true
        };
        context.Doctors.Add(doctor);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task QuickCheck_ReturnsHealthy_ForCleanDatabase()
    {
        await SeedCleanDatabaseAsync();

        var report = await _healthService.RunQuickCheckAsync();

        Assert.True(report.IsHealthy);
        Assert.True(report.QuickCheckPassed);
        Assert.Null(report.ErrorDetails);
        Assert.True(report.DatabaseSizeBytes > 0);
    }

    [Fact]
    public async Task FullIntegrityCheck_ReturnsHealthy_ForCleanDatabase()
    {
        await SeedCleanDatabaseAsync();

        var report = await _healthService.RunFullIntegrityCheckAsync();

        Assert.True(report.IsHealthy);
        Assert.True(report.IntegrityCheckPassed);
        Assert.True(report.SearchIndexHealthy);
    }

    [Fact]
    public void NetworkPathCheck_DetectsUncPaths()
    {
        Assert.True(_healthService.IsNetworkOrUncPath(@"\\server\share\data.db"));
        Assert.True(_healthService.IsNetworkOrUncPath("//192.168.1.50/share/data.db"));
        Assert.False(_healthService.IsNetworkOrUncPath(@"C:\Users\DoctorRx\data.db"));
    }

    [Fact]
    public async Task QuickCheck_DetectsCorruption_AndQuarantineIsCreated()
    {
        await SeedCleanDatabaseAsync();
        SqliteConnection.ClearAllPools();

        // Deliberately corrupt database by overwriting internal database pages
        var dbBytes = await File.ReadAllBytesAsync(_appPaths.DatabasePath);
        Assert.True(dbBytes.Length > 4096);
        // Overwrite page 2 with random/garbage data to corrupt the B-Tree
        for (int i = 100; i < 2000; i++)
        {
            dbBytes[i] = 0xAA;
        }
        await File.WriteAllBytesAsync(_appPaths.DatabasePath, dbBytes);

        var report = await _healthService.RunQuickCheckAsync();
        Assert.False(report.IsHealthy);
        Assert.False(report.QuickCheckPassed);
        Assert.NotNull(report.ErrorDetails);

        // Quarantine the corrupted file
        var quarantinePath = await _healthService.QuarantineCorruptDatabaseAsync();
        Assert.NotNull(quarantinePath);
        Assert.True(File.Exists(quarantinePath));
        Assert.False(File.Exists(_appPaths.DatabasePath));
        Assert.Contains("doctorrx.corrupt-", quarantinePath);
    }

    [Fact]
    public async Task DatabaseInitializer_ThrowsStartupException_WhenCorruptDatabaseDetected()
    {
        await SeedCleanDatabaseAsync();
        SqliteConnection.ClearAllPools();

        // Corrupt database file
        var dbBytes = await File.ReadAllBytesAsync(_appPaths.DatabasePath);
        for (int i = 100; i < 2000; i++)
        {
            dbBytes[i] = 0xFF;
        }
        await File.WriteAllBytesAsync(_appPaths.DatabasePath, dbBytes);

        var initializer = new DatabaseInitializer(
            _migrator,
            _seeder,
            _searchRepair,
            _healthService,
            _appPaths,
            NullLogger<DatabaseInitializer>.Instance);

        var ex = await Assert.ThrowsAsync<DoctorRxStartupException>(() => initializer.InitializeAsync());

        Assert.Equal(StartupFailureReason.DatabaseCorrupt, ex.Reason);
        Assert.Contains("quarantined", ex.UserGuidance, StringComparison.OrdinalIgnoreCase);
        // Original corrupt database should have been moved away
        Assert.False(File.Exists(_appPaths.DatabasePath));
    }

    [Fact]
    public async Task CheckpointWal_ExecutesWithoutError()
    {
        await SeedCleanDatabaseAsync();

        // Must run cleanly without throwing
        await _healthService.CheckpointWalAsync();
    }
}
