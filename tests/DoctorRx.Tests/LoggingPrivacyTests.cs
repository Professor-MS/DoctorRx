using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class TestSinkLogger<T> : ILogger<T>
{
    public ConcurrentBag<string> Messages { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var msg = formatter(state, exception);
        Messages.Add(msg);
        if (exception != null)
        {
            Messages.Add(exception.ToString());
        }
    }
}

public class LoggingPrivacyTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly IDbContextFactory<DoctorRxDbContext> _factory;

    public LoggingPrivacyTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_PrivacyTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task ServiceLogs_NeverContainPatientPII()
    {
        // Arrange
        var uowFactory = new UnitOfWorkFactory(_factory);
        var clock = new SystemClock();

        var patientLogger = new TestSinkLogger<PatientService>();
        var rxLogger = new TestSinkLogger<PrescriptionService>();

        var patientService = new PatientService(uowFactory, clock, patientLogger);
        var rxService = new PrescriptionService(uowFactory, clock, rxLogger);

        const string sensitiveName = "Khadija Bibi Sensitive";
        const string sensitivePhone = "+92 345 9876543";
        const string sensitiveAddress = "Plot 99, Secret Road, Abbottabad";
        const string sensitiveHistory = "Severe cardiac arrhythmia diagnosed";
        const string sensitiveAllergy = "Fatal reaction to Penicillin";

        // Act 1: Create Patient
        var createPatientResult = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            Name = sensitiveName,
            Age = 42,
            Gender = Gender.Female,
            Phone = sensitivePhone,
            Address = sensitiveAddress,
            MedicalHistoryNotes = sensitiveHistory,
            KnownAllergies = sensitiveAllergy
        });
        Assert.True(createPatientResult.IsSuccess);
        var patientId = createPatientResult.Value!.Id;

        // Seed doctor and medicine
        int doctorId;
        int medicineId;
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var doctor = new Doctor
            {
                Name = "Dr. Doctor",
                Qualification = "MBBS",
                RegistrationNumber = "999",
                Specialization = "Physician",
                ClinicName = "Clinic",
                IsActive = true
            };
            var medicine = new Medicine
            {
                Name = "Paracetamol",
                NormalizedName = "paracetamol",
                Form = "Tablet",
                Strength = "500mg"
            };
            context.Doctors.Add(doctor);
            context.Medicines.Add(medicine);
            await context.SaveChangesAsync();
            doctorId = doctor.Id;
            medicineId = medicine.Id;
        }

        // Act 2: Finalize Prescription
        var createRxResult = await rxService.FinalizePrescriptionAsync(new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = clock.Today,
            ChiefComplaints = "High fever",
            ClinicalNotes = "Confidential exam details",
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineId = medicineId,
                    MedicineName = "Paracetamol",
                    Form = "Tablet",
                    Strength = "500mg",
                    Dose = "1 tab",
                    Frequency = "TDS",
                    Route = "Oral",
                    Duration = "3 days"
                }
            }
        });
        Assert.True(createRxResult.IsSuccess);

        // Assert: Aggregate all logged messages across patient and prescription services
        var allLogs = patientLogger.Messages.Concat(rxLogger.Messages).ToList();
        var concatenatedLogs = string.Join("\n", allLogs);

        Assert.DoesNotContain(sensitiveName, concatenatedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sensitivePhone, concatenatedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("923459876543", concatenatedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sensitiveAddress, concatenatedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sensitiveHistory, concatenatedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sensitiveAllergy, concatenatedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Confidential exam details", concatenatedLogs, StringComparison.OrdinalIgnoreCase);
    }
}
