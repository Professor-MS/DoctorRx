using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
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
}

public class PatientServiceTests
{
    private (PatientService service, TestDbContextFactory factory) CreateSut()
    {
        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factory = new TestDbContextFactory(options);
        var uowFactory = new UnitOfWorkFactory(factory);
        var logger = NullLogger<PatientService>.Instance;
        var service = new PatientService(uowFactory, logger);

        return (service, factory);
    }

    [Fact]
    public async Task CreatePatient_WithValidData_ReturnsSuccessAndPersists()
    {
        // Arrange
        var (service, _) = CreateSut();
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
        var result = await service.CreatePatientAsync(dto);

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
        var (service, _) = CreateSut();
        var dto = new CreatePatientDto
        {
            Name = "   ",
            Age = 25
        };

        // Act
        var result = await service.CreatePatientAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Patient name is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreatePatient_WithInvalidAge_ReturnsFailure()
    {
        // Arrange
        var (service, _) = CreateSut();
        var dto = new CreatePatientDto
        {
            Name = "Ahmad",
            Age = 150
        };

        // Act
        var result = await service.CreatePatientAsync(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Age must be between 0 and 130", result.ErrorMessage);
    }

    [Fact]
    public async Task ConcurrentServices_NeverShareDbContextInstance()
    {
        // Arrange
        var (service, factory) = CreateSut();

        // Seed 1 patient
        await service.CreatePatientAsync(new CreatePatientDto { Name = "Patient 1", Age = 30 });

        // Act: Run two concurrent reads
        var task1 = Task.Run(() => service.GetPatientsPagedAsync(1, 50));
        var task2 = Task.Run(() => service.GetPatientsPagedAsync(1, 50));
        await Task.WhenAll(task1, task2);

        // Assert: Distinct DbContext instances were generated
        Assert.True(factory.CreatedContexts.Count >= 3); // 1 for create, 2 for concurrent reads
        var lastTwoContexts = factory.CreatedContexts.GetRange(factory.CreatedContexts.Count - 2, 2);
        Assert.NotSame(lastTwoContexts[0], lastTwoContexts[1]);
    }

    [Fact]
    public async Task FailedSave_DoesNotPoisonSubsequentSave()
    {
        // Arrange
        var (service, factory) = CreateSut();

        // Force a failed save directly through UoW factory
        var uowFactory = new UnitOfWorkFactory(factory);
        await using (var failUow = uowFactory.Create())
        {
            // Adding patient with null required field to induce save error in InMemory
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
        var result = await service.CreatePatientAsync(goodDto);

        // Assert: Succeeds without poison
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Healthy Patient", result.Value.Name);
    }
}
