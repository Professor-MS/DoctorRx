using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.ValueObjects;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class DoctorEvidenceTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly TestDbContextFactory _factory;
    private readonly UnitOfWorkFactory _uowFactory;
    private readonly SystemClock _clock;
    private readonly DoctorService _doctorService;
    private readonly PrescriptionService _prescriptionService;

    public DoctorEvidenceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_DocTests_" + Guid.NewGuid().ToString("N"));
        _appPaths = new TestAppPaths(_testDir);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _appPaths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(builder.ConnectionString)
            .Options;

        _factory = new TestDbContextFactory(options);
        _uowFactory = new UnitOfWorkFactory(_factory);
        _clock = new SystemClock();
        _doctorService = new DoctorService(_uowFactory, _clock, NullLogger<DoctorService>.Instance);
        _prescriptionService = new PrescriptionService(_uowFactory, _clock, NullLogger<PrescriptionService>.Instance);
    }

    public void Dispose()
    {
        foreach (var ctx in _factory.CreatedContexts)
        {
            ctx.Dispose();
        }
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task Migration_PreExistingDuplicateActiveDoctors_ResolvedToSingleActive()
    {
        // Arrange: Migrate up to previous migration (AddDoctorTitleAndRegistrationLabel)
        await using var migContext = await _factory.CreateDbContextAsync();
        var migrator = migContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261009123154_AddDoctorTitleAndRegistrationLabel");

        // Insert two active doctors via raw SQL (bypassing EF Core indexes)
        await using var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}");
        await conn.OpenAsync();
        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
INSERT INTO Doctors (TitlePrefix, Name, Qualification, RegistrationLabel, RegistrationNumber, Specialization, ClinicName, IsActive, CreatedAtUtc)
VALUES ('Dr.', 'Doctor One', 'MBBS', 'Reg. No.', 'REG-001', 'Cardiology', 'Clinic Alpha', 1, '2026-01-01');

INSERT INTO Doctors (TitlePrefix, Name, Qualification, RegistrationLabel, RegistrationNumber, Specialization, ClinicName, IsActive, CreatedAtUtc)
VALUES ('Dr.', 'Doctor Two', 'FCPS', 'Reg. No.', 'REG-002', 'Neurology', 'Clinic Beta', 1, '2026-01-02');
";
        await insertCmd.ExecuteNonQueryAsync();

        // Verify pre-condition: exactly 2 active doctors exist
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(1) FROM Doctors WHERE IsActive = 1;";
        var activeCountBefore = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        Assert.Equal(2, activeCountBefore);

        // Act: Apply latest migration (AddPrescriptionIsSealed) which deduplicates active doctors and creates IX_Doctors_SingleActive
        await migContext.Database.MigrateAsync();

        // Assert 1: Only 1 doctor remains active, the other is set to IsActive = 0
        var activeCountAfter = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        Assert.Equal(1, activeCountAfter);

        // Assert 2: Attempting to insert or activate a second active doctor violates the unique index IX_Doctors_SingleActive
        await using var duplicateCmd = conn.CreateCommand();
        duplicateCmd.CommandText = "UPDATE Doctors SET IsActive = 1 WHERE IsActive = 0;";
        var ex = await Assert.ThrowsAsync<SqliteException>(async () =>
        {
            await duplicateCmd.ExecuteNonQueryAsync();
        });
        Assert.Contains("UNIQUE constraint failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateDoctorAsync_WhenActiveDoctorAlreadyExists_Fails()
    {
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            await ctx.Database.MigrateAsync();
        }

        var dto1 = new CreateDoctorDto
        {
            Name = "Tariq Mahmood",
            Qualification = "MBBS, FCPS",
            RegistrationNumber = "PMDC-12345",
            Specialization = "Cardiologist",
            ClinicName = "City Heart Clinic"
        };

        var res1 = await _doctorService.CreateDoctorAsync(dto1);
        Assert.True(res1.IsSuccess);

        // Act: Attempt to create a second doctor profile while an active one exists
        var dto2 = new CreateDoctorDto
        {
            Name = "Ayesha Khan",
            Qualification = "MBBS",
            RegistrationNumber = "PMDC-67890",
            Specialization = "Pediatrician",
            ClinicName = "Children Care"
        };

        var res2 = await _doctorService.CreateDoctorAsync(dto2);

        // Assert: Must fail with single-active doctor guard
        Assert.False(res2.IsSuccess);
        Assert.Contains("active doctor profile already exists", res2.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SwitchActiveDoctorAsync_SwitchesActiveDoctorSuccessfully()
    {
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            await ctx.Database.MigrateAsync();
        }

        // Create doc 1 active
        var res1 = await _doctorService.CreateDoctorAsync(new CreateDoctorDto
        {
            Name = "Doc One",
            Qualification = "MBBS",
            RegistrationNumber = "R1",
            Specialization = "General",
            ClinicName = "Clinic 1"
        });
        Assert.True(res1.IsSuccess);
        var doc1Id = res1.Value!.Id;

        // Directly add doc 2 as inactive in DB
        int doc2Id;
        await using (var uow = _uowFactory.Create())
        {
            var doc2 = new Doctor
            {
                Name = "Doc Two",
                Qualification = "FCPS",
                RegistrationNumber = "R2",
                Specialization = "Surgery",
                ClinicName = "Clinic 2",
                IsActive = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            await uow.Doctors.AddAsync(doc2);
            await uow.CommitAsync();
            doc2Id = doc2.Id;
        }

        // Verify active doctor is doc 1
        var active1 = await _doctorService.GetActiveDoctorAsync();
        Assert.Equal(doc1Id, active1!.Id);

        // Act: Switch active doctor to doc 2
        var switchRes = await _doctorService.SwitchActiveDoctorAsync(doc2Id);
        Assert.True(switchRes.IsSuccess);

        // Assert: Active doctor is now doc 2
        var active2 = await _doctorService.GetActiveDoctorAsync();
        Assert.Equal(doc2Id, active2!.Id);

        // Verify in DB that doc 1 is inactive
        await using (var uow = _uowFactory.Create())
        {
            var doc1Db = await uow.Doctors.GetByIdAsync(doc1Id);
            Assert.False(doc1Db!.IsActive);
            var doc2Db = await uow.Doctors.GetByIdAsync(doc2Id);
            Assert.True(doc2Db!.IsActive);
        }
    }

    [Theory]
    [InlineData("", "MBBS", "REG-1", "Spec", "Clinic", "Doctor name is required.")]
    [InlineData("Dr. Name", "", "REG-1", "Spec", "Clinic", "Qualification is required.")]
    [InlineData("Dr. Name", "MBBS", "", "Spec", "Clinic", "Registration number is required.")]
    [InlineData("Dr. Name", "MBBS", "REG-1", "", "Clinic", "Specialization is required.")]
    [InlineData("Dr. Name", "MBBS", "REG-1", "Spec", "", "Clinic name is required.")]
    public async Task DoctorService_Validation_RequiredFields_Fail(
        string name, string qual, string regNum, string spec, string clinic, string expectedError)
    {
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            await ctx.Database.MigrateAsync();
        }

        var dto = new CreateDoctorDto
        {
            Name = name,
            Qualification = qual,
            RegistrationNumber = regNum,
            Specialization = spec,
            ClinicName = clinic
        };

        var result = await _doctorService.CreateDoctorAsync(dto);
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.ErrorMessage);
    }

    [Fact]
    public async Task DoctorService_Validation_MaxLengthsAndLightPhoneCheck()
    {
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            await ctx.Database.MigrateAsync();
        }

        // Registration label > 30
        var longLabelDto = new CreateDoctorDto
        {
            Name = "Dr. Valid",
            Qualification = "MBBS",
            RegistrationNumber = "REG-1",
            Specialization = "General",
            ClinicName = "Clinic",
            RegistrationLabel = new string('A', 31)
        };
        var labelRes = await _doctorService.CreateDoctorAsync(longLabelDto);
        Assert.False(labelRes.IsSuccess);
        Assert.Contains("Registration label cannot exceed 30 characters", labelRes.ErrorMessage);

        // Phone < 7 digits fails light check
        var shortPhoneDto = new CreateDoctorDto
        {
            Name = "Dr. Valid",
            Qualification = "MBBS",
            RegistrationNumber = "REG-1",
            Specialization = "General",
            ClinicName = "Clinic",
            Phone = "12345" // only 5 digits
        };
        var phoneRes = await _doctorService.CreateDoctorAsync(shortPhoneDto);
        Assert.False(phoneRes.IsSuccess);
        Assert.Contains("Doctor phone must contain at least 7 digits", phoneRes.ErrorMessage);

        // Valid phone with formatting >= 7 digits succeeds
        var validPhoneDto = new CreateDoctorDto
        {
            Name = "Dr. Valid",
            Qualification = "MBBS",
            RegistrationNumber = "REG-1",
            Specialization = "General",
            ClinicName = "Clinic",
            Phone = "+92 300 1234567" // 12 digits
        };
        var validPhoneRes = await _doctorService.CreateDoctorAsync(validPhoneDto);
        Assert.True(validPhoneRes.IsSuccess);
    }

    [Fact]
    public async Task DoctorProfileFlowTests_EditThroughBadge_PrescriptionPreservesSnapshot_NoDuplicateRow()
    {
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            await ctx.Database.MigrateAsync();
        }

        // Step 1: Create initial doctor profile
        var createRes = await _doctorService.CreateDoctorAsync(new CreateDoctorDto
        {
            TitlePrefix = "Dr.",
            Name = "Original Name",
            Qualification = "MBBS",
            RegistrationLabel = "PMC No.",
            RegistrationNumber = "PMC-1111",
            Specialization = "General Physician",
            ClinicName = "Alpha Medical Center"
        });
        Assert.True(createRes.IsSuccess);
        var doctorId = createRes.Value!.Id;

        // Create a patient
        Patient patient;
        await using (var uow = _uowFactory.Create())
        {
            patient = new Patient
            {
                Name = "John Doe",
                NormalizedName = "john doe",
                Gender = Gender.Male,
                RecordNumber = "P-1001"
            };
            await uow.Patients.AddAsync(patient);
            await uow.CommitAsync();
        }

        // Step 2: Issue Prescription 1 with original profile
        var rx1Dto = new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            DoctorId = doctorId,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.Today),
            ChiefComplaints = "Fever",
            Items = new()
            {
                new CreatePrescriptionMedicineDto
                {
                    MedicineName = "Panadol",
                    Form = "Tablet",
                    Dose = "500mg",
                    Frequency = "TDS",
                    Route = "Oral"
                }
            }
        };
        var rx1Res = await _prescriptionService.FinalizePrescriptionAsync(rx1Dto);
        Assert.True(rx1Res.IsSuccess);
        var rx1Id = rx1Res.Value!.Id;

        // Step 3: Doctor edits profile (e.g. through shell badge)
        var updateRes = await _doctorService.UpdateDoctorAsync(new UpdateDoctorDto
        {
            Id = doctorId,
            TitlePrefix = "Prof. Dr.",
            Name = "Updated Name",
            Qualification = "MBBS, FCPS, FRCP",
            RegistrationLabel = "PMC No.",
            RegistrationNumber = "PMC-1111",
            Specialization = "Consultant Cardiologist",
            ClinicName = "Apex Heart Hospital"
        });
        Assert.True(updateRes.IsSuccess);

        // Step 4: Issue Prescription 2 with updated profile
        var rx2Dto = new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            DoctorId = doctorId,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.Today),
            ChiefComplaints = "Chest Pain",
            Items = new()
            {
                new CreatePrescriptionMedicineDto
                {
                    MedicineName = "Aspirin",
                    Form = "Tablet",
                    Dose = "75mg",
                    Frequency = "OD",
                    Route = "Oral"
                }
            }
        };
        var rx2Res = await _prescriptionService.FinalizePrescriptionAsync(rx2Dto);
        Assert.True(rx2Res.IsSuccess);
        var rx2Id = rx2Res.Value!.Id;

        // Assert: Immutability of historical snapshot
        var rx1 = await _prescriptionService.GetPrescriptionByIdAsync(rx1Id);
        Assert.NotNull(rx1);
        Assert.Equal("Original Name", rx1.DoctorSnapshot.Name);
        Assert.Equal("Dr.", rx1.DoctorSnapshot.TitlePrefix);
        Assert.Equal("Dr. Original Name", rx1.DoctorSnapshot.DisplayName);
        Assert.Equal("Alpha Medical Center", rx1.DoctorSnapshot.ClinicName);
        Assert.Equal("General Physician", rx1.DoctorSnapshot.Specialization);

        // Assert: New prescription reflects updated doctor snapshot
        var rx2 = await _prescriptionService.GetPrescriptionByIdAsync(rx2Id);
        Assert.NotNull(rx2);
        Assert.Equal("Updated Name", rx2.DoctorSnapshot.Name);
        Assert.Equal("Prof. Dr.", rx2.DoctorSnapshot.TitlePrefix);
        Assert.Equal("Prof. Dr. Updated Name", rx2.DoctorSnapshot.DisplayName);
        Assert.Equal("Apex Heart Hospital", rx2.DoctorSnapshot.ClinicName);
        Assert.Equal("Consultant Cardiologist", rx2.DoctorSnapshot.Specialization);

        // Assert: Table Doctors contains exactly 1 row (no duplicate row was inserted)
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            var doctors = await ctx.Doctors.ToListAsync();
            Assert.Single(doctors);
            Assert.Equal(doctorId, doctors[0].Id);
        }
    }

    [Fact]
    public void OldPrescriptions_WithoutTitleOrLabel_FallBackToDefaults()
    {
        // Snapshot without title prefix or registration label
        var snapshot = new DoctorSnapshot
        {
            TitlePrefix = "",
            RegistrationLabel = "",
            Name = "Ali Raza",
            RegistrationNumber = "12345"
        };

        Assert.Equal("Dr.", snapshot.SafeTitlePrefix);
        Assert.Equal("Reg. No.", snapshot.SafeRegistrationLabel);
        Assert.Equal("Dr. Ali Raza", snapshot.DisplayName);
    }
}
