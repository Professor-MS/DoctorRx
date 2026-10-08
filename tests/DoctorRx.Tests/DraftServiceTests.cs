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
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class DraftServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestDbContextFactory _factory;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;
    private readonly DraftService _draftService;
    private readonly PrescriptionService _prescriptionService;

    public DraftServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_DraftTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        var dbPath = Path.Combine(_testDir, "test.db");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(builder.ConnectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _factory = new TestDbContextFactory(options);
        _uowFactory = new UnitOfWorkFactory(_factory);
        _clock = new SystemClock();
        _draftService = new DraftService(_uowFactory, _clock, NullLogger<DraftService>.Instance);
        _prescriptionService = new PrescriptionService(_uowFactory, _clock, NullLogger<PrescriptionService>.Instance, _draftService);
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
        }
    }

    private async Task<(int DoctorId, int PatientId, int MedicineId)> SeedDataAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();

        var doctor = new Doctor
        {
            Name = "Dr. Imran",
            Qualification = "MBBS, FCPS",
            RegistrationNumber = "PMC-7890",
            Specialization = "Cardiologist",
            ClinicName = "Heart Care Clinic",
            IsActive = true
        };
        context.Doctors.Add(doctor);

        var patient = new Patient
        {
            Name = "Ahmed Ali",
            NormalizedName = "ahmed ali",
            Gender = Gender.Male,
            RecordNumber = "P-5001",
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-40)),
            Phone = "03009998877",
            PhoneDigits = "03009998877"
        };
        context.Patients.Add(patient);

        var medicine = new Medicine
        {
            Name = "Augmentin",
            NormalizedName = "augmentin",
            GenericName = "Amoxicillin + Clavulanate",
            Form = "Tablet",
            Strength = "625mg",
            UsageCount = 10,
            LastUsedAtUtc = DateTime.UtcNow.AddDays(-2),
            IsActive = true
        };
        context.Medicines.Add(medicine);

        await context.SaveChangesAsync();
        return (doctor.Id, patient.Id, medicine.Id);
    }

    [Fact]
    public async Task ZombieDraftRace_AutosaveDuringOrAfterFinalize_IsRefusedAndNoDraftRemains()
    {
        // Arrange
        var (doctorId, patientId, medicineId) = await SeedDataAsync();

        var state = new PrescriptionComposerState
        {
            DraftKey = Guid.NewGuid(),
            PatientId = patientId,
            Items = new List<PrescriptionMedicineRowState>
            {
                new()
                {
                    MedicineId = medicineId,
                    MedicineName = "Augmentin",
                    Form = "Tablet",
                    Strength = "625mg",
                    Dose = "1 tab",
                    Frequency = "BD",
                    Duration = "5 days"
                }
            }
        };

        // Save initial draft
        var saveResult = await _draftService.SaveAsync(state);
        Assert.True(saveResult.IsSuccess);

        // Verify draft exists in database
        var draftInDb = await _draftService.GetAsync(state.DraftKey);
        Assert.True(draftInDb.IsSuccess);

        // Act: Finalize starts - marks draft finalized and deletes it atomically
        var finalizeDto = new CreatePrescriptionDto
        {
            DraftKey = state.DraftKey,
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.Today),
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineId = medicineId,
                    MedicineName = "Augmentin",
                    Form = "Tablet",
                    Strength = "625mg",
                    Dose = "1 tab",
                    Frequency = "BD",
                    Duration = "5 days"
                }
            }
        };

        var finalizeResult = await _prescriptionService.FinalizePrescriptionAsync(finalizeDto);
        Assert.True(finalizeResult.IsSuccess);

        // Simulate a late autosave firing right during/after finalize
        state.IsFinalized = true; // Composer marks state finalized
        var lateAutosaveResult = await _draftService.SaveAsync(state);

        // Assert: SaveAsync must refuse to write
        Assert.False(lateAutosaveResult.IsSuccess);
        Assert.Contains("finalized", lateAutosaveResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // And verify no draft remains in the database
        var postFinalizeDraft = await _draftService.GetAsync(state.DraftKey);
        Assert.False(postFinalizeDraft.IsSuccess);

        var allDrafts = await _draftService.ListAsync();
        Assert.DoesNotContain(allDrafts, d => d.DraftKey == state.DraftKey);
    }

    [Fact]
    public async Task FinalizePrescription_AtomicDraftDeletion_FailedFinalizePreservesDraft()
    {
        // Arrange
        var (doctorId, patientId, medicineId) = await SeedDataAsync();

        var draftKey = Guid.NewGuid();
        var state = new PrescriptionComposerState
        {
            DraftKey = draftKey,
            PatientId = patientId,
            Items = new List<PrescriptionMedicineRowState>
            {
                new()
                {
                    MedicineId = medicineId,
                    MedicineName = "Augmentin",
                    Dose = "1 tab",
                    Frequency = "BD"
                }
            }
        };
        await _draftService.SaveAsync(state);

        // Act: Attempt to finalize with an INVALID patient ID (forces transaction failure)
        var invalidDto = new CreatePrescriptionDto
        {
            DraftKey = draftKey,
            PatientId = 999999, // Non-existent patient
            DoctorId = doctorId,
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineId = medicineId,
                    MedicineName = "Augmentin",
                    Dose = "1 tab",
                    Frequency = "BD"
                }
            }
        };

        var finalizeResult = await _prescriptionService.FinalizePrescriptionAsync(invalidDto);

        // Assert: Finalize failed
        Assert.False(finalizeResult.IsSuccess);

        // Crucial: The draft MUST STILL EXIST in the database because transaction rolled back!
        var draft = await _draftService.GetAsync(draftKey);
        Assert.True(draft.IsSuccess);
        Assert.NotNull(draft.Value);
    }

    [Fact]
    public async Task DraftRecovery_RefreshesPatientDisplayFromDatabase_NeverTrustsPayload()
    {
        // Arrange
        var (doctorId, patientId, medicineId) = await SeedDataAsync();

        var draftKey = Guid.NewGuid();
        var state = new PrescriptionComposerState
        {
            DraftKey = draftKey,
            PatientId = patientId,
            PatientName = "Stale Name In Payload",
            PatientRecordNumber = "STALE-001",
            PatientAgeText = "99 yrs",
            Items = new List<PrescriptionMedicineRowState>()
        };

        await _draftService.SaveAsync(state);

        // Now mutate the real patient in the database
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var p = await context.Patients.FindAsync(patientId);
            p!.Name = "Updated Real Name";
            await context.SaveChangesAsync();
        }

        // Act: Recover draft via GetAsync
        var recovered = await _draftService.GetAsync(draftKey);

        // Assert: Patient name and record number must be refreshed from database
        Assert.True(recovered.IsSuccess);
        Assert.Equal("Updated Real Name", recovered.Value!.PatientName);
        Assert.Equal("P-5001", recovered.Value!.PatientRecordNumber);
    }

    [Fact]
    public async Task DraftRecovery_MissingPatientInDb_FallsBackToPayloadDisplay()
    {
        // Arrange: Seed database and save draft with non-existent patient ID
        var (doctorId, patientId, medicineId) = await SeedDataAsync();

        var draftKey = Guid.NewGuid();
        var state = new PrescriptionComposerState
        {
            DraftKey = draftKey,
            PatientId = patientId,
            PatientName = "Deleted Patient Display Fallback",
            PatientRecordNumber = "FALLBACK-100",
            Items = new List<PrescriptionMedicineRowState>()
        };

        var saveResult = await _draftService.SaveAsync(state);
        Assert.True(saveResult.IsSuccess);

        // Now delete the patient from the database to simulate a deleted patient
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var p = await context.Patients.FindAsync(patientId);
            context.Patients.Remove(p!);
            await context.SaveChangesAsync();
        }

        // Act
        var recovered = await _draftService.GetAsync(draftKey);

        // Assert: Falls back gracefully to payload cached strings
        Assert.True(recovered.IsSuccess);
        Assert.Equal("Deleted Patient Display Fallback", recovered.Value!.PatientName);
        Assert.Equal("FALLBACK-100", recovered.Value!.PatientRecordNumber);
    }

    [Fact]
    public async Task ListAsync_FlagsDraftsOlderThan30Days_NeverAutoDeletes()
    {
        // Arrange
        var (doctorId, patientId, medicineId) = await SeedDataAsync();

        var recentKey = Guid.NewGuid();
        var oldKey = Guid.NewGuid();

        var recentState = new PrescriptionComposerState { DraftKey = recentKey, PatientId = patientId };
        var oldState = new PrescriptionComposerState { DraftKey = oldKey, PatientId = patientId };

        await _draftService.SaveAsync(recentState);
        await _draftService.SaveAsync(oldState);

        // Backdate the old draft by 35 days in SQLite
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var oldDraft = await context.Drafts.FirstAsync(d => d.DraftKey == oldKey);
            oldDraft.UpdatedAtUtc = DateTime.UtcNow.AddDays(-35);
            await context.SaveChangesAsync();
        }

        // Act
        var list = await _draftService.ListAsync();

        // Assert
        Assert.Equal(2, list.Count);
        var oldSummary = list.First(d => d.DraftKey == oldKey);
        var recentSummary = list.First(d => d.DraftKey == recentKey);

        Assert.True(oldSummary.IsOlderThan30Days);
        Assert.False(recentSummary.IsOlderThan30Days);
    }

    [Fact]
    public async Task ListAsync_CorruptPayload_DoesNotCrash_ReturnsCorruptSummary()
    {
        // Arrange
        var (doctorId, patientId, medicineId) = await SeedDataAsync();

        var corruptKey = Guid.NewGuid();
        await using (var context = await _factory.CreateDbContextAsync())
        {
            context.Drafts.Add(new Draft
            {
                DraftKey = corruptKey,
                PatientId = patientId,
                PayloadJson = "{ INVALID JSON CORRUPT DATA [[",
                PayloadVersion = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                AppVersion = "1.0.0"
            });
            await context.SaveChangesAsync();
        }

        // Act: Listing must handle corrupt row gracefully without throwing
        var list = await _draftService.ListAsync();

        // Assert
        var corruptItem = list.FirstOrDefault(d => d.DraftKey == corruptKey);
        Assert.NotNull(corruptItem);
        Assert.True(corruptItem.IsCorrupt);
    }

    [Fact]
    public async Task MedicineUsageRanking_FinalizeIncrementsUsage_AutocompleteOrdersByUsage()
    {
        // Arrange
        var (doctorId, patientId, med1Id) = await SeedDataAsync();

        // Seed a second medicine with higher usage count
        int med2Id;
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var med2 = new Medicine
            {
                Name = "Augmentin Forte",
                NormalizedName = "augmentin forte",
                GenericName = "Amoxicillin + Clavulanate",
                Form = "Syrup",
                Strength = "312mg/5ml",
                UsageCount = 50,
                LastUsedAtUtc = DateTime.UtcNow,
                IsActive = true
            };
            context.Medicines.Add(med2);
            await context.SaveChangesAsync();
            med2Id = med2.Id;
        }

        // Finalize a prescription using med1 ("Augmentin", initial UsageCount = 10)
        var dto = new CreatePrescriptionDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.Today),
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineId = med1Id,
                    MedicineName = "Augmentin",
                    Form = "Tablet",
                    Dose = "625mg",
                    Frequency = "BD"
                }
            }
        };

        var result = await _prescriptionService.FinalizePrescriptionAsync(dto);
        Assert.True(result.IsSuccess);

        // Verify med1 usage was incremented to 11
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var med1 = await context.Medicines.FindAsync(med1Id);
            Assert.Equal(11, med1!.UsageCount);
        }

        // Autocomplete search for "augmentin"
        await using (var uow = _uowFactory.Create())
        {
            var searchResults = await uow.Medicines.SearchAsync("augmentin");

            // Augmentin Forte (UsageCount = 50) must appear before Augmentin (UsageCount = 11)
            Assert.True(searchResults.Count >= 2);
            Assert.Equal("Augmentin Forte", searchResults[0].Name);
            Assert.Equal("Augmentin", searchResults[1].Name);
        }
    }
}
