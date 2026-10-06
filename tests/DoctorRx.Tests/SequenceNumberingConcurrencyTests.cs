using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
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

public class SequenceNumberingConcurrencyTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly IDbContextFactory<DoctorRxDbContext> _factory;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;

    public SequenceNumberingConcurrencyTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_SeqTests_" + Guid.NewGuid().ToString("N"));
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
        _uowFactory = new UnitOfWorkFactory(_factory);
        _clock = new SystemClock();

        // Run migrations
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

    private async Task<(int doctorId, int patientId, int medicineId)> SeedBaseEntitiesAsync()
    {
        await using var uow = _uowFactory.Create();

        var doctor = new Doctor
        {
            Name = "Dr. Ahmed Khan",
            Qualification = "MBBS, FCPS",
            RegistrationNumber = "PMC-12345",
            Specialization = "Cardiologist",
            ClinicName = "Apex Heart Clinic",
            IsActive = true
        };
        await uow.Doctors.AddAsync(doctor);

        var patient = new Patient
        {
            RecordNumber = "P-000001",
            Name = "Bilal Tariq",
            NormalizedName = "bilal tariq",
            Gender = Gender.Male,
            Age = 35,
            AgeRecordedDate = _clock.Today,
            CreatedAtUtc = _clock.UtcNow
        };
        await uow.Patients.AddAsync(patient);

        var medicine = new Medicine
        {
            Name = "Amoxicillin",
            NormalizedName = "amoxicillin",
            Form = "Capsule",
            Strength = "500mg"
        };
        await uow.Medicines.AddAsync(medicine);

        await uow.CommitAsync();

        return (doctor.Id, patient.Id, medicine.Id);
    }

    private static CreatePrescriptionDto CreateTestDto(int patientId, int doctorId, int medicineId, DateOnly prescriptionDate)
    {
        return new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = prescriptionDate,
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineId = medicineId,
                    MedicineName = "Amoxicillin",
                    Form = "Capsule",
                    Strength = "500mg",
                    Dose = "1 cap",
                    Frequency = "TDS",
                    Route = "Oral",
                    Duration = "5 days",
                    MealRelation = MealRelation.AfterMeal
                }
            }
        };
    }

    [Fact]
    public async Task SequenceAllocation_RollbackLeavesZeroGaps()
    {
        // Arrange
        var (doctorId, patientId, medicineId) = await SeedBaseEntitiesAsync();
        var today = _clock.Today;

        // Step 1: Finalize 1st prescription successfully
        var rxService = new PrescriptionService(_uowFactory, _clock, NullLogger<PrescriptionService>.Instance);
        var firstResult = await rxService.FinalizePrescriptionAsync(CreateTestDto(patientId, doctorId, medicineId, today));

        Assert.True(firstResult.IsSuccess);
        Assert.EndsWith("-0001", firstResult.Value!.PrescriptionNumber);

        // Step 2: Simulate an operation that allocates sequence #2 on the write transaction, but rolls back
        await using (var uow = _uowFactory.Create())
        await using (var tx = await uow.BeginWriteTransactionAsync())
        {
            var nextNum = await uow.Prescriptions.GenerateNextPrescriptionNumberAsync(today);
            Assert.EndsWith("-0002", nextNum);

            // Intentionally rollback
            await tx.RollbackAsync();
        }

        // Step 3: Now finalize 2nd prescription through service: it must receive #0002 (ZERO GAPS!)
        var secondResult = await rxService.FinalizePrescriptionAsync(CreateTestDto(patientId, doctorId, medicineId, today));

        Assert.True(secondResult.IsSuccess);
        Assert.EndsWith("-0002", secondResult.Value!.PrescriptionNumber);
    }

    [Fact]
    public async Task PrescriptionFinalize_50ConcurrentOperations_AllocatesUniqueSequentialNumbersWithoutGapsOrCollisions()
    {
        // Arrange
        var (doctorId, patientId, medicineId) = await SeedBaseEntitiesAsync();
        var today = _clock.Today;
        var rxService = new PrescriptionService(_uowFactory, _clock, NullLogger<PrescriptionService>.Instance);

        const int concurrentCount = 50;
        var tasks = new List<Task<Application.Common.Result<PrescriptionDetailDto>>>();

        for (int i = 0; i < concurrentCount; i++)
        {
            var dto = CreateTestDto(patientId, doctorId, medicineId, today);
            tasks.Add(Task.Run(() => rxService.FinalizePrescriptionAsync(dto)));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.True(r.IsSuccess, r.ErrorMessage));

        var numbers = results.Select(r => r.Value!.PrescriptionNumber).ToList();
        var distinctNumbers = numbers.Distinct().ToList();

        // Exactly 50 distinct prescription numbers
        Assert.Equal(concurrentCount, distinctNumbers.Count);

        // All numbers must match RX-yyyyMMdd-0001 through RX-yyyyMMdd-0050
        var expectedPrefix = $"RX-{today:yyyyMMdd}-";
        Assert.All(numbers, n => Assert.StartsWith(expectedPrefix, n));

        var sequenceIntegers = numbers
            .Select(n => int.Parse(n.Substring(expectedPrefix.Length)))
            .OrderBy(n => n)
            .ToList();

        for (int i = 0; i < concurrentCount; i++)
        {
            Assert.Equal(i + 1, sequenceIntegers[i]);
        }
    }

    [Fact]
    public async Task PrescriptionFinalizeAndCancel_UpdatesAndRecalculatesPatientLastVisitDate()
    {
        // Arrange
        var (doctorId, patientId, medicineId) = await SeedBaseEntitiesAsync();
        var date1 = new DateOnly(2026, 10, 1);
        var date2 = new DateOnly(2026, 10, 5);
        var rxService = new PrescriptionService(_uowFactory, _clock, NullLogger<PrescriptionService>.Instance);

        // Step 1: Finalize Rx on date1
        var rx1Result = await rxService.FinalizePrescriptionAsync(CreateTestDto(patientId, doctorId, medicineId, date1));
        Assert.True(rx1Result.IsSuccess);

        // Verify patient last visit date is date1
        await using (var uow = _uowFactory.Create())
        {
            var patient = await uow.Patients.GetByIdAsync(patientId);
            Assert.NotNull(patient);
            Assert.Equal(date1, patient!.LastVisitDate);
        }

        // Step 2: Finalize Rx on date2
        var rx2Result = await rxService.FinalizePrescriptionAsync(CreateTestDto(patientId, doctorId, medicineId, date2));
        Assert.True(rx2Result.IsSuccess);

        // Verify patient last visit date updated to date2
        await using (var uow = _uowFactory.Create())
        {
            var patient = await uow.Patients.GetByIdAsync(patientId);
            Assert.NotNull(patient);
            Assert.Equal(date2, patient!.LastVisitDate);
        }

        // Step 3: Cancel Rx2 -> last visit date recalculates back to date1
        var cancel2Result = await rxService.CancelPrescriptionAsync(rx2Result.Value!.Id, "Incorrect dosage entered");
        Assert.True(cancel2Result.IsSuccess);

        await using (var uow = _uowFactory.Create())
        {
            var patient = await uow.Patients.GetByIdAsync(patientId);
            Assert.NotNull(patient);
            Assert.Equal(date1, patient!.LastVisitDate);
        }

        // Step 4: Cancel Rx1 -> last visit date becomes null (no remaining active finalized prescriptions)
        var cancel1Result = await rxService.CancelPrescriptionAsync(rx1Result.Value!.Id, "Patient did not collect");
        Assert.True(cancel1Result.IsSuccess);

        await using (var uow = _uowFactory.Create())
        {
            var patient = await uow.Patients.GetByIdAsync(patientId);
            Assert.NotNull(patient);
            Assert.Null(patient!.LastVisitDate);
        }
    }
}
