using System;
using System.IO;
using System.Threading.Tasks;
using DoctorRx.Application.Interfaces;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class DemoDataSeedingTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly IDbContextFactory<DoctorRxDbContext> _factory;
    private readonly string? _originalEnv;

    public DemoDataSeedingTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_SeedTests_" + Guid.NewGuid().ToString("N"));
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
        _originalEnv = Environment.GetEnvironmentVariable("DOCTORRX_DEMO");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DOCTORRX_DEMO", _originalEnv);
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
    public async Task InitializeAsync_WhenDemoFlagNotSet_DoesNotSeedAnyEntities()
    {
        // Arrange: Explicitly clear the environment variable
        Environment.SetEnvironmentVariable("DOCTORRX_DEMO", null);

        var migrator = new DatabaseMigrator(_factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        var seeder = new DemoDataSeeder(_factory, NullLogger<DemoDataSeeder>.Instance);
        var repairService = new SearchIndexRepairService(_factory, NullLogger<SearchIndexRepairService>.Instance);
        var initializer = new DatabaseInitializer(migrator, seeder, repairService, NullLogger<DatabaseInitializer>.Instance);

        // Act
        await initializer.InitializeAsync();

        // Assert: Database has tables migrated, but zero seed records
        await using var context = await _factory.CreateDbContextAsync();
        Assert.Equal(0, await context.Doctors.CountAsync());
        Assert.Equal(0, await context.Patients.CountAsync());
        Assert.Equal(0, await context.Medicines.CountAsync());
        Assert.Equal(0, await context.Prescriptions.CountAsync());
    }

    [Fact]
    public async Task InitializeAsync_WhenDemoFlagIs1_SeedsAllDemoEntities()
    {
        // Arrange: Explicitly enable DOCTORRX_DEMO=1
        Environment.SetEnvironmentVariable("DOCTORRX_DEMO", "1");

        var migrator = new DatabaseMigrator(_factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        var seeder = new DemoDataSeeder(_factory, NullLogger<DemoDataSeeder>.Instance);
        var repairService = new SearchIndexRepairService(_factory, NullLogger<SearchIndexRepairService>.Instance);
        var initializer = new DatabaseInitializer(migrator, seeder, repairService, NullLogger<DatabaseInitializer>.Instance);

        // Act
        await initializer.InitializeAsync();

        // Assert: All demo records are seeded
        await using var context = await _factory.CreateDbContextAsync();
        Assert.True(await context.Doctors.AnyAsync());
        Assert.True(await context.Patients.AnyAsync());
        Assert.True(await context.Medicines.AnyAsync());
        Assert.True(await context.Prescriptions.AnyAsync());

        var doctor = await context.Doctors.FirstAsync();
        Assert.Equal("Dr. Muhammad Tariq", doctor.Name);
    }

    [Fact]
    public async Task DemoDataSeeder_SeedAsync_IsIdempotent()
    {
        // Arrange
        var migrator = new DatabaseMigrator(_factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        await migrator.MigrateDatabaseAsync();

        var seeder = new DemoDataSeeder(_factory, NullLogger<DemoDataSeeder>.Instance);

        // Act: Run seed twice
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert: Records are not duplicated
        await using var context = await _factory.CreateDbContextAsync();
        Assert.Equal(1, await context.Doctors.CountAsync());
        Assert.Equal(3, await context.Patients.CountAsync());
        Assert.Equal(10, await context.Medicines.CountAsync());
        Assert.Equal(1, await context.Prescriptions.CountAsync());
    }
}
