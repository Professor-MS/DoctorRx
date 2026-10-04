using System;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Enums;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class PatientServiceTests
{
    private (PatientService service, DoctorRxDbContext context) CreateSut()
    {
        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new DoctorRxDbContext(options);
        var patientRepo = new PatientRepository(context);
        var rxRepo = new PrescriptionRepository(context);
        var medRepo = new MedicineRepository(context);
        var docRepo = new DoctorRepository(context);

        var uow = new UnitOfWork(context, patientRepo, rxRepo, medRepo, docRepo);
        var logger = NullLogger<PatientService>.Instance;
        var service = new PatientService(uow, logger);

        return (service, context);
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
}
