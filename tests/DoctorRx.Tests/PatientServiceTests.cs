using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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

namespace DoctorRx.Tests;

public class TestDbContextFactory : IDbContextFactory<DoctorRxDbContext>
{
    private readonly DbContextOptions<DoctorRxDbContext> _options;
    public List<DoctorRxDbContext> CreatedContexts { get; } = new();

    public TestDbContextFactory(DbContextOptions<DoctorRxDbContext> options)
    {
        _options = options;
    }

    public DoctorRxDbContext CreateDbContext()
    {
        var context = new DoctorRxDbContext(_options);
        lock (CreatedContexts)
        {
            CreatedContexts.Add(context);
        }
        return context;
    }

    public Task<DoctorRxDbContext> CreateDbContextAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }
}

public class PatientServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly TestDbContextFactory _factory;
    private readonly PatientService _service;

    public PatientServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_PatTests_" + Guid.NewGuid().ToString("N"));
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

        var uowFactory = new UnitOfWorkFactory(_factory);
        var logger = NullLogger<PatientService>.Instance;
        var clock = new SystemClock();
        _service = new PatientService(uowFactory, clock, logger);
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
    public async Task CreatePatient_WithValidData_ReturnsSuccessAndPersists()
    {
        // Arrange
        var dto = new CreatePatientDto
        {
            Name = "Muhammad Ali",
            Age = 35,
            Gender = Gender.Male,
            Phone = "+92 300 9998877",
            Address = "Islamabad",
            KnownAllergies = "None"
        };

        // Act
        var result = await _service.CreatePatientAsync(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Id > 0);
        Assert.Equal("Muhammad Ali", result.Value.Name);
        Assert.Equal(35, result.Value.Age);
        Assert.Equal(Gender.Male, result.Value.Gender);
    }

    [Fact]
    public async Task CreatePatient_WithEmptyName_ReturnsFailure()
    {
        // Arrange
        var dto = new CreatePatientDto
        {
            Name = "   ",
            Age = 25
        };

        // Act
        var result = await _service.CreatePatientAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Patient name is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreatePatient_WithInvalidAge_ReturnsFailure()
    {
        // Arrange
        var dto = new CreatePatientDto
        {
            Name = "Ahmad",
            Age = 150
        };

        // Act
        var result = await _service.CreatePatientAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Age must be between 0 and 130", result.ErrorMessage);
    }

    [Fact]
    public async Task ConcurrentServices_NeverShareDbContextInstance()
    {
        // Arrange: Seed 1 patient
        await _service.CreatePatientAsync(new CreatePatientDto { Name = "Patient 1", Age = 30 });

        // Act: Run two concurrent reads
        var task1 = Task.Run(() => _service.GetPatientsPagedAsync(1, 50));
        var task2 = Task.Run(() => _service.GetPatientsPagedAsync(1, 50));
        await Task.WhenAll(task1, task2);

        // Assert: Distinct DbContext instances were generated
        Assert.True(_factory.CreatedContexts.Count >= 3); // 1 for create, 2 for concurrent reads
        var lastTwoContexts = _factory.CreatedContexts.GetRange(_factory.CreatedContexts.Count - 2, 2);
        Assert.NotSame(lastTwoContexts[0], lastTwoContexts[1]);
    }

    [Fact]
    public async Task FailedSave_DoesNotPoisonSubsequentSave()
    {
        // Arrange: Force a failed save directly through UoW factory
        var uowFactory = new UnitOfWorkFactory(_factory);
        await using (var failUow = uowFactory.Create())
        {
            // Adding patient with null required field to induce save error in SQLite (NOT NULL constraint)
            var brokenPatient = new Patient { Name = null! };
            await failUow.Patients.AddAsync(brokenPatient);
            await Assert.ThrowsAnyAsync<Exception>(() => failUow.CommitAsync());
        }

        // Act: Next call through the stateless service with fresh context
        var goodDto = new CreatePatientDto
        {
            Name = "Healthy Patient",
            Age = 40,
            Gender = Gender.Female
        };
        var result = await _service.CreatePatientAsync(goodDto);

        // Assert: Succeeds without poison
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Healthy Patient", result.Value.Name);
    }
}
