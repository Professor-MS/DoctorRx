using System;
using System.IO;
using System.Threading.Tasks;
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

public class ForeignKeyEnforcementTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly IDbContextFactory<DoctorRxDbContext> _factory;

    public ForeignKeyEnforcementTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_FKTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task ForeignKey_DeletingPrescribedMedicine_ThrowsSqliteExceptionWithRestrict()
    {
        // Arrange
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var doctor = new Doctor
            {
                Name = "Dr. Test",
                Qualification = "MBBS",
                RegistrationNumber = "123",
                Specialization = "GP",
                ClinicName = "Clinic",
                IsActive = true
            };
            var patient = new Patient
            {
                RecordNumber = "P-000001",
                Name = "Patient",
                NormalizedName = "patient",
                Gender = Gender.Male,
                Age = 40,
                CreatedAtUtc = DateTime.UtcNow
            };
            var medicine = new Medicine
            {
                Name = "Paracetamol",
                NormalizedName = "paracetamol",
                Form = "Tablet",
                Strength = "500mg"
            };

            context.Doctors.Add(doctor);
            context.Patients.Add(patient);
            context.Medicines.Add(medicine);
            await context.SaveChangesAsync();

            var rx = Prescription.CreateFinalized(
                prescriptionNumber: "RX-20261006-0001",
                patientId: patient.Id,
                doctorId: doctor.Id,
                prescriptionDate: new DateOnly(2026, 10, 6),
                doctorSnapshot: doctor.ToSnapshot(),
                patientSnapshot: patient.ToSnapshot(new DateOnly(2026, 10, 6)),
                finalizedAtUtc: DateTime.UtcNow
            );

            rx.AddMedicine(new PrescriptionMedicine
            {
                MedicineId = medicine.Id,
                MedicineName = medicine.Name,
                Form = medicine.Form,
                Strength = medicine.Strength,
                Dose = "1 tab",
                Frequency = "TDS"
            });

            context.Prescriptions.Add(rx);
            await context.SaveChangesAsync();
        }

        // Act & Assert: Attempting to delete the prescribed medicine must fail due to ON DELETE RESTRICT
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var med = await context.Medicines.FirstAsync(m => m.Name == "Paracetamol");
            context.Medicines.Remove(med);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("FOREIGN KEY constraint failed", ex.InnerException?.Message ?? ex.Message);
        }
    }

    [Fact]
    public async Task ForeignKey_InsertingPrescriptionMedicineWithInvalidMedicineId_ThrowsSqliteException()
    {
        // Arrange
        int patientId;
        int doctorId;
        int rxId;

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var doctor = new Doctor
            {
                Name = "Dr. Test",
                Qualification = "MBBS",
                RegistrationNumber = "123",
                Specialization = "GP",
                ClinicName = "Clinic",
                IsActive = true
            };
            var patient = new Patient
            {
                RecordNumber = "P-000001",
                Name = "Patient",
                NormalizedName = "patient",
                Gender = Gender.Male,
                Age = 40,
                CreatedAtUtc = DateTime.UtcNow
            };
            context.Doctors.Add(doctor);
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            patientId = patient.Id;
            doctorId = doctor.Id;

            var rx = Prescription.CreateFinalized(
                prescriptionNumber: "RX-20261006-0001",
                patientId: patientId,
                doctorId: doctorId,
                prescriptionDate: new DateOnly(2026, 10, 6),
                doctorSnapshot: doctor.ToSnapshot(),
                patientSnapshot: patient.ToSnapshot(new DateOnly(2026, 10, 6)),
                finalizedAtUtc: DateTime.UtcNow
            );
            context.Prescriptions.Add(rx);
            await context.SaveChangesAsync();
            rxId = rx.Id;
        }

        // Act & Assert: Insert PrescriptionMedicine with non-existent MedicineId = 99999
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var item = new PrescriptionMedicine
            {
                PrescriptionId = rxId,
                MedicineId = 99999, // Does not exist
                MedicineName = "Ghost Med",
                Form = "Tablet",
                Strength = "10mg",
                Dose = "1 tab",
                Frequency = "OD"
            };

            await context.PrescriptionMedicines.AddAsync(item);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("FOREIGN KEY constraint failed", ex.InnerException?.Message ?? ex.Message);
        }
    }
}
