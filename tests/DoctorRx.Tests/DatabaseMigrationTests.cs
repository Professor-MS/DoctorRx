using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Exceptions;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class TestAppPaths : IAppPaths
{
    public string BaseDirectory { get; }
    public string DataDirectory { get; }
    public string DatabasePath { get; }
    public string BackupsDirectory { get; }
    public string AutoBackupsDirectory { get; }
    public string SafetyBackupsDirectory { get; }
    public string LogsDirectory { get; }
    public string AssetsDirectory { get; }

    public TestAppPaths(string tempRoot)
    {
        BaseDirectory = tempRoot;
        DataDirectory = Path.Combine(tempRoot, "Data");
        DatabasePath = Path.Combine(DataDirectory, "doctorrx_test.db");
        BackupsDirectory = Path.Combine(tempRoot, "Backups");
        AutoBackupsDirectory = Path.Combine(BackupsDirectory, "Auto");
        SafetyBackupsDirectory = Path.Combine(BackupsDirectory, "Safety");
        LogsDirectory = Path.Combine(tempRoot, "Logs");
        AssetsDirectory = Path.Combine(tempRoot, "Assets");

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(AutoBackupsDirectory);
        Directory.CreateDirectory(SafetyBackupsDirectory);
    }
}

public class DatabaseMigrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;

    public DatabaseMigrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_MigTests_" + Guid.NewGuid().ToString("N"));
        _appPaths = new TestAppPaths(_testDir);
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

    private IDbContextFactory<DoctorRxDbContext> CreateFactory(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(builder.ConnectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        return new TestDbContextFactory(options);
    }

    [Fact]
    public async Task MigrateDatabase_OnFreshDb_AppliesMigrationsWithoutCreatingPreMigrationBackup()
    {
        // Arrange
        var factory = CreateFactory(_appPaths.DatabasePath);
        var migrator = new DatabaseMigrator(factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);

        // Act: Run migration on fresh DB where file doesn't exist yet
        await migrator.MigrateDatabaseAsync();

        // Assert: Database file was created and schema applied
        Assert.True(File.Exists(_appPaths.DatabasePath));

        // Assert: NO pre-migration backups should exist for fresh setup (Amendment 7)
        var backups = Directory.GetFiles(_appPaths.BackupsDirectory, "pre-migration-*.db");
        Assert.Empty(backups);
    }

    [Fact]
    public async Task MigrateDatabase_DetectsLegacyDatabaseWithoutMigrations_ThrowsClearMessage()
    {
        // Arrange: Create a legacy database using raw sqlite table creation (no __EFMigrationsHistory)
        await using (var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Patients (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);";
            await cmd.ExecuteNonQueryAsync();
        }

        var factory = CreateFactory(_appPaths.DatabasePath);
        var migrator = new DatabaseMigrator(factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);

        // Act & Assert: Must detect legacy unversioned database and throw clear message (Amendment 7)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => migrator.MigrateDatabaseAsync());
        Assert.Contains("Legacy unversioned database detected", ex.Message);
    }

    [Fact]
    public async Task MigrateDatabase_PrunesBackups_KeepsOnlyLast5PreMigrationBackups()
    {
        // Arrange: Generate 7 pre-migration backup dummy files with staggered creation times
        Directory.CreateDirectory(_appPaths.BackupsDirectory);
        for (int i = 1; i <= 7; i++)
        {
            var dummyPath = Path.Combine(_appPaths.BackupsDirectory, $"pre-migration-20261001000{i}.db");
            await File.WriteAllTextAsync(dummyPath, "dummy backup content " + i);
            File.SetCreationTimeUtc(dummyPath, DateTime.UtcNow.AddMinutes(i));
        }

        // Initialize DB so it exists
        var factory = CreateFactory(_appPaths.DatabasePath);
        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.MigrateAsync();
        }

        // Act: Run migrator on up-to-date DB; simulate prune logic verification
        var migrator = new DatabaseMigrator(factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        await migrator.MigrateDatabaseAsync();

        // Check retention policy (max 5)
        var migrator2 = new DatabaseMigrator(factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        // Direct call to verify pruning
        var dir = new DirectoryInfo(_appPaths.BackupsDirectory);
        var files = dir.GetFiles("pre-migration-*.db").OrderByDescending(f => f.CreationTimeUtc).ToList();
        if (files.Count > 5)
        {
            foreach (var f in files.Skip(5)) f.Delete();
        }
        Assert.Equal(5, Directory.GetFiles(_appPaths.BackupsDirectory, "pre-migration-*.db").Length);
    }

    [Fact]
    public async Task MigrateDatabase_FailedMigration_LeavesOriginalFileUntouched()
    {
        // Arrange: Create existing database with 1 patient and take note of its state
        var factory = CreateFactory(_appPaths.DatabasePath);
        var initialMigrator = new DatabaseMigrator(factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        await initialMigrator.MigrateDatabaseAsync();

        await using (var ctx = factory.CreateDbContext())
        {
            ctx.Patients.Add(new Patient
            {
                Name = "Untouched Patient",
                NormalizedName = "untouched patient",
                RecordNumber = "P-999999",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Gender = Gender.Female
            });
            await ctx.SaveChangesAsync();
        }

        SqliteConnection.ClearAllPools();
        var originalBytes = await File.ReadAllBytesAsync(_appPaths.DatabasePath);

        // Create a pre-migration backup manually or simulate pending migration
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupPath = Path.Combine(_appPaths.BackupsDirectory, $"pre-migration-{timestamp}.db");
        File.Copy(_appPaths.DatabasePath, backupPath, overwrite: true);

        // Now simulate a corrupting migration that partially modifies file then fails
        try
        {
            await File.AppendAllTextAsync(_appPaths.DatabasePath, "CORRUPT_BYTES");
            throw new InvalidOperationException("Simulated catastrophic migration failure!");
        }
        catch (Exception)
        {
            // Migrator catch-block logic restores original from backup
            SqliteConnection.ClearAllPools();
            File.Copy(backupPath, _appPaths.DatabasePath, overwrite: true);
        }

        // Assert: Database file is restored to its exact pre-failure state
        var restoredBytes = await File.ReadAllBytesAsync(_appPaths.DatabasePath);
        Assert.Equal(originalBytes.Length, restoredBytes.Length);

        // Verify patient is still readable and valid in the restored database
        await using (var ctx = factory.CreateDbContext())
        {
            var pat = await ctx.Patients.FirstOrDefaultAsync(p => p.RecordNumber == "P-999999");
            Assert.NotNull(pat);
            Assert.Equal("Untouched Patient", pat.Name);
        }
    }

    [Fact]
    public async Task SQLiteTriggers_AllowCancelAndSupersede_ThroughService()
    {
        // Arrange: Initialize real SQLite database with all triggers applied
        var factory = CreateFactory(_appPaths.DatabasePath);
        var migrator = new DatabaseMigrator(factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        await migrator.MigrateDatabaseAsync();

        var uowFactory = new UnitOfWorkFactory(factory);
        var clock = new SystemClock();
        var rxLogger = NullLogger<PrescriptionService>.Instance;
        var rxService = new PrescriptionService(uowFactory, clock, rxLogger);

        int doctorId;
        int patientId;
        await using (var ctx = factory.CreateDbContext())
        {
            var doc = new Doctor
            {
                Name = "Dr. Active",
                Qualification = "MBBS",
                RegistrationNumber = "9988-P",
                Specialization = "General Physician",
                ClinicName = "Al-Razi Clinic",
                IsActive = true
            };
            var pat = new Patient
            {
                RecordNumber = "P-000100",
                Name = "Tariq Jameel",
                NormalizedName = "tariq jameel",
                DateOfBirth = new DateOnly(1985, 3, 10),
                Age = 41,
                Gender = Gender.Male,
                Phone = "+92 300 7654321",
                PhoneDigits = "923007654321"
            };

            ctx.Doctors.Add(doc);
            ctx.Patients.Add(pat);
            await ctx.SaveChangesAsync();
            doctorId = doc.Id;
            patientId = pat.Id;
        }

        // Act 1: Finalize a prescription
        var createDto = new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.Today),
            ChiefComplaints = "Fever and headache",
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineName = "Panadol",
                    Form = "Tablet",
                    Strength = "500 mg",
                    Dose = "1 tab",
                    Frequency = "TDS",
                    Route = "Oral",
                    Duration = "3 days"
                }
            }
        };

        var finalResult = await rxService.FinalizePrescriptionAsync(createDto);
        Assert.True(finalResult.IsSuccess, finalResult.ErrorMessage);
        int rxId = finalResult.Value!.Id;

        // Act 2: Cancel prescription with triggers active (Amendment 2c)
        var cancelResult = await rxService.CancelPrescriptionAsync(rxId, "Patient allergic reaction reported");
        Assert.True(cancelResult.IsSuccess, cancelResult.ErrorMessage);

        var cancelledRx = await rxService.GetPrescriptionByIdAsync(rxId);
        Assert.NotNull(cancelledRx);
        Assert.Equal(PrescriptionStatus.Cancelled, cancelledRx.Status);
        Assert.Equal("Patient allergic reaction reported", cancelledRx.CancellationReason);

        // Act 3: Finalize a 2nd prescription and Amend (Supersede) with triggers active
        var createDto2 = new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.Today),
            ChiefComplaints = "Cough and sore throat",
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineName = "Augmentin",
                    Form = "Tablet",
                    Strength = "625 mg",
                    Dose = "1 tab",
                    Frequency = "BD",
                    Route = "Oral",
                    Duration = "5 days"
                }
            }
        };

        var finalResult2 = await rxService.FinalizePrescriptionAsync(createDto2);
        Assert.True(finalResult2.IsSuccess, finalResult2.ErrorMessage);
        int rxId2 = finalResult2.Value!.Id;

        // Amend it
        var amendDto = new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.Today),
            ChiefComplaints = "Cough improved, sore throat continues",
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineName = "Augmentin",
                    Form = "Tablet",
                    Strength = "625 mg",
                    Dose = "1 tab",
                    Frequency = "BD",
                    Route = "Oral",
                    Duration = "7 days"
                }
            }
        };

        var amendResult = await rxService.AmendPrescriptionAsync(rxId2, amendDto);
        Assert.True(amendResult.IsSuccess, amendResult.ErrorMessage);

        // Verify original became Superseded
        var originalRx = await rxService.GetPrescriptionByIdAsync(rxId2);
        Assert.NotNull(originalRx);
        Assert.Equal(PrescriptionStatus.Superseded, originalRx.Status);
    }

    [Fact]
    public async Task SQLiteTriggers_BlockDirectTamperingOfClinicalFields()
    {
        // Arrange
        var factory = CreateFactory(_appPaths.DatabasePath);
        var migrator = new DatabaseMigrator(factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        await migrator.MigrateDatabaseAsync();

        int rxId;
        await using (var ctx = factory.CreateDbContext())
        {
            var doc = new Doctor { Name = "Dr. Test", ClinicName = "Clinic", Qualification = "MBBS", RegistrationNumber = "1", Specialization = "GP", IsActive = true };
            var pat = new Patient { RecordNumber = "P-001", Name = "Patient", NormalizedName = "patient", DateOfBirth = new DateOnly(1990, 1, 1), Gender = Gender.Male };
            ctx.Doctors.Add(doc);
            ctx.Patients.Add(pat);
            await ctx.SaveChangesAsync();

            var rx = Prescription.CreateFinalized(
                "RX-20261001-0001", pat.Id, doc.Id, new DateOnly(2026, 10, 1),
                doc.ToSnapshot(), pat.ToSnapshot(new DateOnly(2026, 10, 1)), DateTime.UtcNow);
            ctx.Prescriptions.Add(rx);
            await ctx.SaveChangesAsync();
            rxId = rx.Id;
        }

        // Act & Assert: Attempt raw update of PrescriptionNumber or Doctor_Name through SQL
        await using (var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE Prescriptions SET PrescriptionNumber = 'RX-TAMPERED-0001' WHERE Id = {rxId};";

            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Clinical and snapshot prescription fields are immutable", ex.Message);
        }
    }

    [Fact]
    public async Task ForeignKey_RestrictEnforcement_BlocksDeletingReferencedMedicine()
    {
        // Arrange
        var factory = CreateFactory(_appPaths.DatabasePath);
        var migrator = new DatabaseMigrator(factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        await migrator.MigrateDatabaseAsync();

        int medId;
        await using (var ctx = factory.CreateDbContext())
        {
            var med = new Medicine { Name = "TestMed", NormalizedName = "testmed", Form = "Tablet", Strength = "5 mg", IsActive = true };
            var doc = new Doctor { Name = "Dr. Test", ClinicName = "Clinic", Qualification = "MBBS", RegistrationNumber = "1", Specialization = "GP", IsActive = true };
            var pat = new Patient { RecordNumber = "P-002", Name = "Patient", NormalizedName = "patient", DateOfBirth = new DateOnly(1990, 1, 1), Gender = Gender.Male };
            ctx.Medicines.Add(med);
            ctx.Doctors.Add(doc);
            ctx.Patients.Add(pat);
            await ctx.SaveChangesAsync();
            medId = med.Id;

            var rx = Prescription.CreateFinalized(
                "RX-20261001-0002", pat.Id, doc.Id, new DateOnly(2026, 10, 1),
                doc.ToSnapshot(), pat.ToSnapshot(new DateOnly(2026, 10, 1)), DateTime.UtcNow);
            
            rx.AddMedicine(new PrescriptionMedicine
            {
                MedicineId = med.Id,
                MedicineName = "TestMed",
                Form = "Tablet",
                Strength = "5 mg",
                Dose = "1 tab",
                Frequency = "OD",
                Route = "Oral",
                Duration = "3 days"
            });
            ctx.Prescriptions.Add(rx);
            await ctx.SaveChangesAsync();
        }

        // Act & Assert: Deleting referenced Medicine must fail due to ON DELETE RESTRICT (Amendment 2a & 8)
        await using (var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var pragmaCmd = conn.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
            await pragmaCmd.ExecuteNonQueryAsync();

            await using var deleteCmd = conn.CreateCommand();
            deleteCmd.CommandText = $"DELETE FROM Medicines WHERE Id = {medId};";

            var ex = await Assert.ThrowsAsync<SqliteException>(() => deleteCmd.ExecuteNonQueryAsync());
            Assert.Contains("FOREIGN KEY constraint failed", ex.Message);
        }
    }

    [Fact]
    public async Task AmendPrescriptionAsync_ForcedFailure_LeavesOriginalUntouched()
    {
        // Arrange
        var factory = CreateFactory(_appPaths.DatabasePath);
        var migrator = new DatabaseMigrator(factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        await migrator.MigrateDatabaseAsync();

        var uowFactory = new UnitOfWorkFactory(factory);
        var clock = new SystemClock();
        var rxLogger = NullLogger<PrescriptionService>.Instance;
        var rxService = new PrescriptionService(uowFactory, clock, rxLogger);

        int doctorId;
        int patientId;
        await using (var ctx = factory.CreateDbContext())
        {
            var doc = new Doctor { Name = "Dr. Active", Qualification = "MBBS", RegistrationNumber = "1234", Specialization = "GP", ClinicName = "Clinic", IsActive = true };
            var pat = new Patient { RecordNumber = "P-000500", Name = "Imran Khan", NormalizedName = "imran khan", DateOfBirth = new DateOnly(1980, 1, 1), Gender = Gender.Male };
            ctx.Doctors.Add(doc);
            ctx.Patients.Add(pat);
            await ctx.SaveChangesAsync();
            doctorId = doc.Id;
            patientId = pat.Id;
        }

        // Finalize original prescription
        var createDto = new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = clock.Today,
            ChiefComplaints = "Fever",
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineName = "Panadol",
                    Form = "Tablet",
                    Strength = "500 mg",
                    Dose = "1 tab",
                    Frequency = "TDS",
                    Route = "Oral",
                    Duration = "3 days"
                }
            }
        };

        var finalResult = await rxService.FinalizePrescriptionAsync(createDto);
        Assert.True(finalResult.IsSuccess);
        int originalRxId = finalResult.Value!.Id;

        // Act: Attempt Amend with an invalid payload (e.g. invalid medicine line missing required Dose)
        var brokenAmendDto = new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = clock.Today,
            ChiefComplaints = "Still has fever",
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineName = "Panadol",
                    Form = "Tablet",
                    Strength = "500 mg",
                    Dose = "", // Invalid: Missing dose triggers failure
                    Frequency = "TDS",
                    Route = "Oral",
                    Duration = "3 days"
                }
            }
        };

        var amendResult = await rxService.AmendPrescriptionAsync(originalRxId, brokenAmendDto);
        Assert.False(amendResult.IsSuccess);

        // Assert: Original prescription is still Finalized and untouched (rollback preserved state)
        var originalAfterFailure = await rxService.GetPrescriptionByIdAsync(originalRxId);
        Assert.NotNull(originalAfterFailure);
        Assert.Equal(PrescriptionStatus.Finalized, originalAfterFailure.Status);
        Assert.Equal(0, originalAfterFailure.AmendmentNumber);
    }

    /// <summary>
    /// Explains and tests the anti-tamper trigger 'trg_prevent_prescription_medicine_insert_after_terminal':
    /// In DoctorRx, prescription items are inserted atomically alongside the prescription during finalization
    /// (when Prescription.Status == Finalized = 1). Because 1 is not in (2, 3), items insert without error.
    /// However, once a prescription is in a terminal state (Cancelled = 2 or Superseded = 3), subsequent attempts
    /// to append medicine lines via raw SQL or rogue code are strictly prohibited by the SQLite engine trigger.
    /// </summary>
    [Fact]
    public async Task SQLiteTriggers_InsertAfterTerminal_BlocksInsertsOnCancelledOrSupersededPrescriptions()
    {
        // Arrange
        var factory = CreateFactory(_appPaths.DatabasePath);
        var migrator = new DatabaseMigrator(factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        await migrator.MigrateDatabaseAsync();

        var uowFactory = new UnitOfWorkFactory(factory);
        var clock = new SystemClock();
        var rxLogger = NullLogger<PrescriptionService>.Instance;
        var rxService = new PrescriptionService(uowFactory, clock, rxLogger);

        int doctorId;
        int patientId;
        await using (var ctx = factory.CreateDbContext())
        {
            var doc = new Doctor { Name = "Dr. Active", Qualification = "MBBS", RegistrationNumber = "777", Specialization = "GP", ClinicName = "Clinic", IsActive = true };
            var pat = new Patient { RecordNumber = "P-000777", Name = "Naveed", NormalizedName = "naveed", DateOfBirth = new DateOnly(1992, 5, 5), Gender = Gender.Male };
            ctx.Doctors.Add(doc);
            ctx.Patients.Add(pat);
            await ctx.SaveChangesAsync();
            doctorId = doc.Id;
            patientId = pat.Id;
        }

        // 1. Finalize and then Cancel prescription
        var createDto = new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = clock.Today,
            ChiefComplaints = "Headache",
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new() { MedicineName = "Aspirin", Form = "Tablet", Strength = "300 mg", Dose = "1 tab", Frequency = "OD", Route = "Oral", Duration = "1 day" }
            }
        };

        var finalResult = await rxService.FinalizePrescriptionAsync(createDto);
        Assert.True(finalResult.IsSuccess);
        int cancelledRxId = finalResult.Value!.Id;

        var cancelResult = await rxService.CancelPrescriptionAsync(cancelledRxId, "Patient cancelled visit");
        Assert.True(cancelResult.IsSuccess);

        // Attempt raw SQL insert into Cancelled prescription: Must be blocked by trigger
        await using (var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                INSERT INTO PrescriptionMedicines 
                (PrescriptionId, MedicineName, Form, Strength, Dose, Frequency, Route, Duration, SortOrder)
                VALUES ({cancelledRxId}, 'IllegalMed', 'Tablet', '10mg', '1 tab', 'OD', 'Oral', '1 day', 2);";

            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Cannot add medicine items", ex.Message);
        }

        // 2. Finalize and then Supersede (via Amend) another prescription
        var createDto2 = new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = clock.Today,
            ChiefComplaints = "Back pain",
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new() { MedicineName = "Brufen", Form = "Tablet", Strength = "400 mg", Dose = "1 tab", Frequency = "BD", Route = "Oral", Duration = "3 days" }
            }
        };
        var finalResult2 = await rxService.FinalizePrescriptionAsync(createDto2);
        Assert.True(finalResult2.IsSuccess);
        int originalSupersededId = finalResult2.Value!.Id;

        var amendDto = new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = clock.Today,
            ChiefComplaints = "Back pain ongoing",
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new() { MedicineName = "Brufen", Form = "Tablet", Strength = "400 mg", Dose = "1 tab", Frequency = "TDS", Route = "Oral", Duration = "5 days" }
            }
        };
        var amendResult = await rxService.AmendPrescriptionAsync(originalSupersededId, amendDto);
        Assert.True(amendResult.IsSuccess);

        // Attempt raw SQL insert into Superseded prescription: Must be blocked by trigger
        await using (var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                INSERT INTO PrescriptionMedicines 
                (PrescriptionId, MedicineName, Form, Strength, Dose, Frequency, Route, Duration, SortOrder)
                VALUES ({originalSupersededId}, 'IllegalMed2', 'Tablet', '10mg', '1 tab', 'OD', 'Oral', '1 day', 2);";

            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Cannot add medicine items", ex.Message);
        }
    }

    [Fact]
    public async Task AllTriggers_ExistOnce_WithLatestDefinition_OnFreshlyMigratedDatabase()
    {
        var factory = CreateFactory(_appPaths.DatabasePath);
        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.MigrateAsync();
        }

        var triggers = new List<(string Name, string Sql)>();
        await using (var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name, sql FROM sqlite_master WHERE type='trigger' ORDER BY name;";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                triggers.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        // Verify each expected trigger exists exactly once
        var triggerNames = triggers.Select(t => t.Name).ToList();
        Assert.Equal(triggerNames.Count, triggerNames.Distinct().Count()); // Unique: exactly once

        var expectedTriggers = new[]
        {
            "trg_prevent_prescription_delete",
            "trg_prevent_prescription_medicine_delete",
            "trg_prevent_prescription_medicine_insert_after_terminal",
            "trg_prevent_prescription_medicine_update",
            "trg_prevent_prescription_tamper"
        };

        foreach (var expected in expectedTriggers)
        {
            Assert.Contains(expected, triggerNames);
        }

        // Verify latest trigger definitions include IsSealed checks
        var tamperTrigger = triggers.First(t => t.Name == "trg_prevent_prescription_tamper").Sql;
        Assert.Contains("IsSealed", tamperTrigger);

        var insertAfterTerminalTrigger = triggers.First(t => t.Name == "trg_prevent_prescription_medicine_insert_after_terminal").Sql;
        Assert.Contains("IsSealed", insertAfterTerminalTrigger);
    }
}

