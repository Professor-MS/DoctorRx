using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.ValueObjects;
using DoctorRx.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DoctorRx.Tests;

public class AntiTamperSchemaDrivenTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dbPath;
    private readonly TestDbContextFactory _factory;

    public AntiTamperSchemaDrivenTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_AntiTamper_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(builder.ConnectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _factory = new TestDbContextFactory(options);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
            // Best effort cleanup in temp
        }
    }

    [Fact]
    public async Task AntiTamperTrigger_SchemaDriven_AllColumnsExceptAllowedTransitions_AbortOnUpdate()
    {
        // Arrange: Run migrations on SQLite
        await using var initContext = await _factory.CreateDbContextAsync();
        await initContext.Database.MigrateAsync();

        // Seed doctor, patient, medicine, and a finalized prescription
        var doctor = new Doctor
        {
            Name = "Dr. Test",
            Qualification = "MBBS",
            RegistrationNumber = "REG-12345",
            Specialization = "General Physician",
            ClinicName = "Test Clinic",
            IsActive = true
        };
        initContext.Doctors.Add(doctor);

        var patient = new Patient
        {
            Name = "John Tamper",
            NormalizedName = "john tamper",
            Gender = Gender.Male,
            RecordNumber = "P-1001",
            Phone = "03001234567",
            PhoneDigits = "03001234567"
        };
        initContext.Patients.Add(patient);
        await initContext.SaveChangesAsync();

        var prescriptionDate = DateOnly.FromDateTime(DateTime.Today);
        var rx = Prescription.CreateFinalized(
            prescriptionNumber: "RX-20261008-0001",
            patientId: patient.Id,
            doctorId: doctor.Id,
            prescriptionDate: prescriptionDate,
            doctorSnapshot: doctor.ToSnapshot(),
            patientSnapshot: patient.ToSnapshot(prescriptionDate),
            finalizedAtUtc: DateTime.UtcNow,
            chiefComplaints: "Original Complaint",
            followUpDate: prescriptionDate.AddDays(7),
            followUpText: "Follow-up in 1 week"
        );
        rx.AddMedicine(new PrescriptionMedicine
        {
            MedicineName = "Amoxicillin",
            Form = "Capsule",
            Dose = "500mg",
            Frequency = "TDS",
            Route = "Oral",
            Duration = "5 days"
        });

        initContext.Prescriptions.Add(rx);
        await initContext.SaveChangesAsync();

        var rxId = rx.Id;

        // Allowed transition columns that can be updated during Cancel or Supersede transitions
        var allowedTransitionColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Status",
            "CancelledAtUtc",
            "CancellationReason",
            "UpdatedAtUtc",
            "Version"
        };

        // Act & Assert: Introspect every column via PRAGMA table_info('Prescriptions')
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var columns = new List<(string Name, string Type)>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Prescriptions');";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var colName = reader.GetString(1);
                var colType = reader.GetString(2);
                columns.Add((colName, colType));
            }
        }

        Assert.NotEmpty(columns);
        Assert.Contains(columns, c => c.Name.Equals("FollowUpText", StringComparison.OrdinalIgnoreCase));

        int testedColumnsCount = 0;

        foreach (var (colName, colType) in columns)
        {
            if (allowedTransitionColumns.Contains(colName))
            {
                continue;
            }

            testedColumnsCount++;

            // Attempt to mutate this column on the finalized row
            await using var mutateCmd = connection.CreateCommand();
            mutateCmd.CommandText = $"UPDATE Prescriptions SET [{colName}] = @val WHERE Id = {rxId};";

            var dummyValue = GetDummyTamperValue(colName, colType);
            mutateCmd.Parameters.AddWithValue("@val", dummyValue);

            SqliteException ex;
            try
            {
                ex = await Assert.ThrowsAsync<SqliteException>(async () =>
                {
                    await mutateCmd.ExecuteNonQueryAsync();
                });
            }
            catch (Exception)
            {
                throw new Exception($"Column '{colName}' (Type: {colType}) did not trigger anti-tamper abort on update!");
            }

            Assert.True(
                ex.Message.Contains("immutable", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("terminal", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("abort", StringComparison.OrdinalIgnoreCase),
                $"Column '{colName}' update did not abort with anti-tamper message. Got: {ex.Message}");
        }

        Assert.True(testedColumnsCount >= 15, $"Expected at least 15 protected columns, tested {testedColumnsCount}");
    }

    [Fact]
    public async Task AntiTamperTrigger_AllowedTransitions_CancelAndSupersede_Succeed()
    {
        // Arrange
        await using var initContext = await _factory.CreateDbContextAsync();
        await initContext.Database.MigrateAsync();

        var doctor = new Doctor { Name = "Dr. Test", Qualification = "MBBS", RegistrationNumber = "REG-999", ClinicName = "Clinic", IsActive = true };
        initContext.Doctors.Add(doctor);
        var patient = new Patient { Name = "Jane Test", NormalizedName = "jane test", Gender = Gender.Female, RecordNumber = "P-1002" };
        initContext.Patients.Add(patient);
        await initContext.SaveChangesAsync();

        var rx = Prescription.CreateFinalized(
            prescriptionNumber: "RX-20261008-0002",
            patientId: patient.Id,
            doctorId: doctor.Id,
            prescriptionDate: DateOnly.FromDateTime(DateTime.Today),
            doctorSnapshot: doctor.ToSnapshot(),
            patientSnapshot: patient.ToSnapshot(DateOnly.FromDateTime(DateTime.Today)),
            finalizedAtUtc: DateTime.UtcNow,
            followUpText: "Follow-up in 3 days"
        );
        rx.AddMedicine(new PrescriptionMedicine { MedicineName = "Paracetamol", Form = "Tablet", Dose = "500mg", Frequency = "SOS" });
        initContext.Prescriptions.Add(rx);
        await initContext.SaveChangesAsync();

        // Act: Valid cancellation transition (updating Status, CancelledAtUtc, CancellationReason, UpdatedAtUtc, Version)
        rx.Cancel("Prescription written in error", DateTime.UtcNow);
        initContext.Prescriptions.Update(rx);
        var exception = await Record.ExceptionAsync(async () => await initContext.SaveChangesAsync());

        // Assert: Cancellation must succeed cleanly
        Assert.Null(exception);
        Assert.Equal(PrescriptionStatus.Cancelled, rx.Status);
    }

    private static object GetDummyTamperValue(string colName, string colType)
    {
        if (colType.Contains("INT", StringComparison.OrdinalIgnoreCase))
        {
            return 99999;
        }
        if (colType.Contains("REAL", StringComparison.OrdinalIgnoreCase) || colType.Contains("FLOA", StringComparison.OrdinalIgnoreCase))
        {
            return 999.9;
        }
        return $"Tampered_{Guid.NewGuid():N}";
    }
}
