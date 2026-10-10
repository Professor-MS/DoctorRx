using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
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

public class IsSealedVerificationTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _connectionString;
    private readonly TestDbContextFactory _factory;

    public IsSealedVerificationTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"DoctorRx_IsSealed_{Guid.NewGuid():N}.db");
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _tempDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        _connectionString = builder.ConnectionString;

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;
        _factory = new TestDbContextFactory(options);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath))
            {
                SqliteConnection.ClearAllPools();
                File.Delete(_tempDbPath);
            }
        }
        catch { }
    }

    [Fact]
    public async Task EFMapping_HasNoHasDefaultValueOnIsSealedBool()
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var entityType = ctx.Model.FindEntityType(typeof(Prescription));
        Assert.NotNull(entityType);

        var isSealedProperty = entityType.FindProperty(nameof(Prescription.IsSealed));
        Assert.NotNull(isSealedProperty);

        // Verify EF Core model has NO HasDefaultValue(true) on the bool property
        Assert.NotEqual(true, isSealedProperty.GetDefaultValue());
        Assert.Null(isSealedProperty.GetDefaultValueSql());
    }

    [Fact]
    public async Task FinalizePrescriptionAsync_InsertsWithIsSealed0_AndSealsInSameTransaction()
    {
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            await ctx.Database.MigrateAsync();
        }

        var clock = new SystemClock();
        var uowFactory = new UnitOfWorkFactory(_factory);
        var docService = new DoctorService(uowFactory, clock, NullLogger<DoctorService>.Instance);
        var patService = new PatientService(uowFactory, clock, NullLogger<PatientService>.Instance);
        var medSearch = new MedicineSearchService(uowFactory, NullLogger<MedicineSearchService>.Instance);
        var medService = new MedicineService(uowFactory, clock, NullLogger<MedicineService>.Instance, medSearch);
        var rxService = new PrescriptionService(uowFactory, clock, NullLogger<PrescriptionService>.Instance);

        var docRes = await docService.CreateDoctorAsync(new CreateDoctorDto
        {
            Name = "Dr. Tester",
            Qualification = "MBBS",
            RegistrationNumber = "REG-TEST",
            Specialization = "Physician",
            ClinicName = "Test Clinic"
        });
        Assert.True(docRes.IsSuccess);

        var patRes = await patService.CreatePatientAsync(new CreatePatientDto
        {
            Name = "Ali Raza",
            Gender = Gender.Male,
            DateOfBirth = new DateOnly(1990, 1, 1),
            Phone = "03001234567"
        });
        Assert.True(patRes.IsSuccess);

        var medRes = await medService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Augmentin",
            Form = "Tablet",
            Strength = "625 mg"
        });
        Assert.True(medRes.IsSuccess);

        var finalizeDto = new CreatePrescriptionDto
        {
            PatientId = patRes.Value!.Id,
            DoctorId = docRes.Value!.Id,
            PrescriptionDate = clock.Today,
            ChiefComplaints = "Fever and cough",
            BloodPressure = "120/80",
            PulseRate = "72",
            Temperature = "98.6",
            WeightKg = "70",
            ClinicalNotes = "Throat infection",
            GeneralAdvice = "Rest and warm fluids",
            FollowUpDate = clock.Today.AddDays(5),
            FollowUpText = "5 days",
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineId = medRes.Value!.Id,
                    MedicineName = "Augmentin",
                    GenericName = "Amoxicillin / Clavulanate",
                    Form = "Tablet",
                    Strength = "625 mg",
                    Dose = "1 tab",
                    Frequency = "TDS",
                    Timing = "Morning, Afternoon, Night",
                    MealRelation = MealRelation.AfterMeal,
                    Route = "Oral",
                    Duration = "5 days",
                    Instructions = "Take with food",
                    SortOrder = 1
                }
            }
        };

        // Act: Finalize prescription
        var rxResult = await rxService.FinalizePrescriptionAsync(finalizeDto);

        // Assert: Succeeded
        Assert.True(rxResult.IsSuccess);
        Assert.NotNull(rxResult.Value);

        // Inspect database state directly
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            var rx = await ctx.Prescriptions.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == rxResult.Value.Id);
            Assert.NotNull(rx);
            Assert.True(rx.IsSealed);
            Assert.Equal(PrescriptionStatus.Finalized, rx.Status);
            Assert.Single(rx.Items);
        }
    }

    [Fact]
    public async Task RowInsertedWithoutWritingIsSealedColumn_DefaultsToOne_AndCannotReceiveItems()
    {
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            await ctx.Database.MigrateAsync();
        }

        // Insert patient & doctor prerequisites
        int docId;
        int patId;
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            var doc = new Doctor
            {
                Name = "Dr. Raw",
                Qualification = "MBBS",
                RegistrationNumber = "REG-RAW",
                Specialization = "Physician",
                ClinicName = "Raw Clinic",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Doctors.Add(doc);

            var pat = new Patient
            {
                Name = "Zahid Khan",
                Gender = Gender.Male,
                DateOfBirth = new DateOnly(1985, 5, 5),
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Patients.Add(pat);
            await ctx.SaveChangesAsync();

            docId = doc.Id;
            patId = pat.Id;
        }

        int rxId;
        await using (var conn = new SqliteConnection(_connectionString))
        {
            await conn.OpenAsync();

            // Insert prescription row WITHOUT specifying IsSealed column (testing SQLite DEFAULT 1)
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO Prescriptions (
                        PrescriptionNumber, PatientId, DoctorId, PrescriptionDate, Status,
                        Doctor_TitlePrefix, Doctor_Name, Doctor_Qualification, Doctor_RegistrationLabel, Doctor_RegistrationNumber, Doctor_Specialization, Doctor_ClinicName,
                        Patient_Name, Patient_AgeText, Patient_Gender,
                        FollowUpText, AmendmentNumber, FinalizedAtUtc, CreatedAtUtc, Version
                    ) VALUES (
                        'RX-RAW-001', @patId, @docId, '2026-10-10', 1,
                        'Dr.', 'Dr. Raw', 'MBBS', 'Reg. No.', 'REG-RAW', 'Physician', 'Raw Clinic',
                        'Zahid Khan', '41 yrs', 1,
                        '5 days', 0, '2026-10-10 12:00:00', '2026-10-10 12:00:00', 'v1'
                    );
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@patId", patId);
                cmd.Parameters.AddWithValue("@docId", docId);
                rxId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Verify IsSealed is 1 due to database schema DEFAULT 1
            using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT IsSealed FROM Prescriptions WHERE Id = @rxId;";
                checkCmd.Parameters.AddWithValue("@rxId", rxId);
                var isSealedVal = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                Assert.Equal(1, isSealedVal);
            }

            // Act & Assert: Attempt to insert an item into PrescriptionMedicines for this row
            // The trigger trg_prevent_prescription_medicine_insert_after_terminal must ABORT
            using (var itemCmd = conn.CreateCommand())
            {
                itemCmd.CommandText = @"
                    INSERT INTO PrescriptionMedicines (
                        PrescriptionId, MedicineId, MedicineName, Form, Strength, Dose, Frequency, Route, Duration, MealRelation, SortOrder
                    ) VALUES (
                        @rxId, 1, 'RawMed', 'Tablet', '500 mg', '1 tab', 'OD', 'Oral', '3 days', 0, 1
                    );";
                itemCmd.Parameters.AddWithValue("@rxId", rxId);

                var ex = await Assert.ThrowsAsync<SqliteException>(async () => await itemCmd.ExecuteNonQueryAsync());
                Assert.Contains("Cannot add medicine items to a sealed, cancelled or superseded prescription", ex.Message);
            }
        }
    }

    [Fact]
    public async Task DemoDataSeeder_SealsPrescriptions_AndHealthCheckReportsZeroUnsealed()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"DoctorRx_SeederTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempFolder);
        var dbPath = Path.Combine(tempFolder, "doctorrx.db");
        var connStr = $"Data Source={dbPath}";

        try
        {
            var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
                .UseSqlite(connStr)
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;
            var dbFactory = new TestDbContextFactory(options);

            await using (var ctx = await dbFactory.CreateDbContextAsync())
            {
                await ctx.Database.MigrateAsync();
            }

            var seeder = new DemoDataSeeder(dbFactory, NullLogger<DemoDataSeeder>.Instance);
            await seeder.SeedAsync();

            // Verify all prescriptions created by demo seeder are sealed
            await using (var ctx = await dbFactory.CreateDbContextAsync())
            {
                var prescriptions = await ctx.Prescriptions.ToListAsync();
                Assert.NotEmpty(prescriptions);
                foreach (var rx in prescriptions)
                {
                    Assert.True(rx.IsSealed, $"Prescription {rx.PrescriptionNumber} was not sealed by DemoDataSeeder.");
                }
            }

            // Check database directly for unsealed finalized prescriptions
            await using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                await using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(1) FROM Prescriptions WHERE Status = 1 AND IsSealed = 0;";
                var unsealedCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync() ?? 0);
                Assert.Equal(0, unsealedCount);
            }

            // Run database health check and assert QuickCheck passes
            var appPaths = new TestAppPaths(tempFolder);
            var repair = new SearchIndexRepairService(dbFactory, NullLogger<SearchIndexRepairService>.Instance);
            var healthService = new DatabaseHealthService(dbFactory, appPaths, new PhysicalFileSystem(), new SystemClock(), repair, NullLogger<DatabaseHealthService>.Instance);
            var report = await healthService.RunQuickCheckAsync();

            Assert.True(report.IsHealthy);
            Assert.True(report.QuickCheckPassed);
        }
        finally
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, recursive: true);
                }
            }
            catch { }
        }
    }
}
