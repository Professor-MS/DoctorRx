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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class MedicineManagementTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly TestDbContextFactory _factory;
    private readonly UnitOfWorkFactory _uowFactory;
    private readonly SystemClock _clock;
    private readonly MedicineSearchService _searchService;
    private readonly MedicineService _medicineService;
    private readonly PrescriptionService _prescriptionService;

    public MedicineManagementTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_MedTests_" + Guid.NewGuid().ToString("N"));
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
        using (var initCtx = _factory.CreateDbContext())
        {
            initCtx.Database.Migrate();
        }

        _uowFactory = new UnitOfWorkFactory(_factory);
        _clock = new SystemClock();
        _searchService = new MedicineSearchService(_uowFactory, NullLogger<MedicineSearchService>.Instance);
        _medicineService = new MedicineService(_uowFactory, _clock, NullLogger<MedicineService>.Instance, _searchService);
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

    private async Task<Doctor> SeedDoctorAsync()
    {
        await using var uow = _uowFactory.Create();
        var doctor = new Doctor
        {
            Name = "Dr. Asim Farooq",
            TitlePrefix = "Dr.",
            Qualification = "MBBS, FCPS",
            RegistrationLabel = "PMC Reg.",
            RegistrationNumber = "78910-P",
            Specialization = "General Physician",
            ClinicName = "Family Healthcare",
            ClinicAddress = "123 Medical Boulevard",
            ClinicPhone = "03001234567",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await uow.Doctors.AddAsync(doctor);
        await uow.CommitAsync();
        return doctor;
    }

    private async Task<Patient> SeedPatientAsync()
    {
        await using var uow = _uowFactory.Create();
        var patient = new Patient
        {
            RecordNumber = "P-1001",
            Name = "Muhammad Ali",
            Age = 35,
            Gender = Gender.Male,
            Phone = "03001234567",
            CreatedAtUtc = DateTime.UtcNow
        };
        patient.RefreshSearchFields();
        await uow.Patients.AddAsync(patient);
        await uow.CommitAsync();
        return patient;
    }

    [Fact]
    public async Task MedicineSearchService_PrefixAndTokenMatch_ReturnsMatches()
    {
        // Arrange
        await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Panadol",
            GenericName = "Paracetamol",
            Form = "Tablet",
            Strength = "500 mg"
        });

        await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Augmentin",
            GenericName = "Amoxicillin Clavulanate",
            Form = "Tablet",
            Strength = "625 mg"
        });

        // Act - Search by brand prefix
        var brandMatches = await _searchService.SearchAsync("Pana");
        // Act - Search by generic token
        var genericMatches = await _searchService.SearchAsync("Amox");

        // Assert
        Assert.Single(brandMatches);
        Assert.Equal("Panadol", brandMatches[0].Name);

        Assert.Single(genericMatches);
        Assert.Equal("Augmentin", genericMatches[0].Name);
    }

    [Fact]
    public async Task MedicineSearchService_DosageFormFilter_FiltersAccurately()
    {
        // Arrange
        await _medicineService.CreateMedicineAsync(new CreateMedicineDto { Name = "Brufen", Form = "Tablet", Strength = "400 mg" });
        await _medicineService.CreateMedicineAsync(new CreateMedicineDto { Name = "Brufen Syrup", Form = "Syrup", Strength = "100 mg/5ml" });
        await _medicineService.CreateMedicineAsync(new CreateMedicineDto { Name = "Rocephin", Form = "Injection", Strength = "1 g" });

        // Act
        var syrupResults = await _searchService.SearchAsync(new MedicineSearchCriteria
        {
            Query = "Brufen",
            DosageForm = "Syrup"
        });

        // Assert
        Assert.Single(syrupResults);
        Assert.Equal("Brufen Syrup", syrupResults[0].Name);
        Assert.Equal("Syrup", syrupResults[0].Form);
    }

    [Fact]
    public async Task MedicineSearchService_ExcludeInactive_ByDefault()
    {
        // Arrange
        var created = await _medicineService.CreateMedicineAsync(new CreateMedicineDto { Name = "DiscontinuedMed", Form = "Tablet", Strength = "10 mg" });
        Assert.True(created.IsSuccess);

        // Deactivate it
        await _medicineService.DeleteMedicineAsync(created.Value!.Id);

        // Act - default search excludes inactive
        var activeSearch = await _searchService.SearchAsync("DiscontinuedMed");
        // Act - search with IncludeInactive = true
        var allSearch = await _searchService.SearchAsync(new MedicineSearchCriteria
        {
            Query = "DiscontinuedMed",
            IncludeInactive = true
        });

        // Assert
        Assert.Empty(activeSearch);
        Assert.Single(allSearch);
        Assert.False(allSearch[0].IsActive);
    }

    [Fact]
    public async Task MedicineService_DuplicateCheck_BlocksExactNameFormAndStrength()
    {
        // Arrange
        var first = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Amoxil",
            GenericName = "Amoxicillin",
            Form = "Capsule",
            Strength = "500 mg"
        });
        Assert.True(first.IsSuccess);

        // Act - Attempt duplicate
        var dup = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Amoxil",
            GenericName = "Amoxicillin",
            Form = "Capsule",
            Strength = "500 mg"
        });

        // Assert
        Assert.False(dup.IsSuccess);
        Assert.Equal(ResultErrorCode.DuplicateWarning, dup.ErrorCode);
        Assert.DoesNotContain("DUPLICATE_WARNING", dup.ErrorMessage ?? string.Empty);
        Assert.Contains("already exists in the catalog", dup.ErrorMessage);
    }

    [Fact]
    public async Task MedicineService_DuplicateCheck_DetectsSpacingDifferencesInStrength()
    {
        // Arrange - "500 mg" vs "500mg"
        var first = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Panadol",
            Form = "Tablet",
            Strength = "500 mg"
        });
        Assert.True(first.IsSuccess);

        // Act - Attempt duplicate with "500mg"
        var dup = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Panadol",
            Form = "Tablet",
            Strength = "500mg"
        });

        // Assert
        Assert.False(dup.IsSuccess);
        Assert.Equal(ResultErrorCode.DuplicateWarning, dup.ErrorCode);
        Assert.DoesNotContain("DUPLICATE_WARNING", dup.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task MedicineService_DuplicateCheck_InactiveMedicine_ReturnsSpecificReactivationWarning()
    {
        // Arrange
        var first = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Calpol",
            Form = "Syrup",
            Strength = "120 mg/5ml"
        });
        Assert.True(first.IsSuccess);

        // Deactivate
        await _medicineService.DeleteMedicineAsync(first.Value!.Id);

        // Act - Attempt to re-create
        var dup = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Calpol",
            Form = "Syrup",
            Strength = "120 mg/5ml"
        });

        // Assert
        Assert.False(dup.IsSuccess);
        Assert.Equal(ResultErrorCode.DuplicateWarning, dup.ErrorCode);
        Assert.DoesNotContain("DUPLICATE_WARNING", dup.ErrorMessage ?? string.Empty);
        Assert.Contains("inactive medicine", dup.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reactivate", dup.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MedicineService_DuplicateCheck_DifferentStrengthOrForm_AllowedWithoutWarning()
    {
        // Arrange
        var first = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Augmentin",
            Form = "Tablet",
            Strength = "375 mg"
        });
        Assert.True(first.IsSuccess);

        // Act - Different strength (625 mg)
        var second = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Augmentin",
            Form = "Tablet",
            Strength = "625 mg"
        });

        // Act - Different form (Syrup)
        var third = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Augmentin",
            Form = "Syrup",
            Strength = "156.25 mg/5ml"
        });

        // Assert
        Assert.True(second.IsSuccess);
        Assert.True(third.IsSuccess);
    }

    [Fact]
    public async Task MedicineService_Create_WithAllowDuplicate_Succeeds()
    {
        // Arrange
        await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Omega3",
            Form = "Capsule",
            Strength = "1000 mg"
        });

        // Act - Save with allowDuplicate = true
        var second = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Omega3",
            Form = "Capsule",
            Strength = "1000 mg"
        }, allowDuplicate: true);

        // Assert
        Assert.True(second.IsSuccess);
        Assert.NotEqual(0, second.Value!.Id);
    }

    [Fact]
    public async Task MedicineService_Update_ClashingWithOtherMedicine_TriggersDuplicateWarning()
    {
        // Arrange
        var medA = await _medicineService.CreateMedicineAsync(new CreateMedicineDto { Name = "MedA", Form = "Tablet", Strength = "10 mg" });
        var medB = await _medicineService.CreateMedicineAsync(new CreateMedicineDto { Name = "MedB", Form = "Tablet", Strength = "10 mg" });
        Assert.True(medA.IsSuccess);
        Assert.True(medB.IsSuccess);

        // Act - Try to rename medB to MedA 10mg
        var update = await _medicineService.UpdateMedicineAsync(new UpdateMedicineDto
        {
            Id = medB.Value!.Id,
            Name = "MedA",
            Form = "Tablet",
            Strength = "10 mg",
            IsActive = true
        });

        // Assert
        Assert.False(update.IsSuccess);
        Assert.Equal(ResultErrorCode.DuplicateWarning, update.ErrorCode);
        Assert.DoesNotContain("DUPLICATE_WARNING", update.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task MedicineService_Update_SameMedicineWithoutClash_Succeeds()
    {
        // Arrange
        var med = await _medicineService.CreateMedicineAsync(new CreateMedicineDto { Name = "Klaricid", Form = "Tablet", Strength = "500 mg" });
        Assert.True(med.IsSuccess);

        // Act - Update Klaricid with same name and form but updated strength
        var update = await _medicineService.UpdateMedicineAsync(new UpdateMedicineDto
        {
            Id = med.Value!.Id,
            Name = "Klaricid",
            GenericName = "Clarithromycin",
            Form = "Tablet",
            Strength = "500 mg",
            IsActive = true
        });

        // Assert
        Assert.True(update.IsSuccess);
        Assert.Equal("Clarithromycin", update.Value!.GenericName);
    }

    [Fact]
    public async Task MedicineService_Delete_WhenReferencedInPrescription_SafelyDeactivatesPreservingHistory()
    {
        // Arrange
        var doctor = await SeedDoctorAsync();
        var patient = await SeedPatientAsync();

        var med = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Lipitor",
            GenericName = "Atorvastatin",
            Form = "Tablet",
            Strength = "20 mg"
        });
        Assert.True(med.IsSuccess);
        int medId = med.Value!.Id;

        // Finalize a prescription using Lipitor
        var finalizeResult = await _prescriptionService.FinalizePrescriptionAsync(new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.Today),
            Items =
            [
                new CreatePrescriptionMedicineDto
                {
                    MedicineId = medId,
                    MedicineName = "Lipitor",
                    GenericName = "Atorvastatin",
                    Form = "Tablet",
                    Strength = "20 mg",
                    Dose = "1 tab",
                    Frequency = "At bedtime",
                    Route = "Oral",
                    Duration = "30 days"
                }
            ]
        });
        Assert.True(finalizeResult.IsSuccess);

        // Act - Delete Lipitor
        var deleteResult = await _medicineService.DeleteMedicineAsync(medId);

        // Assert
        Assert.True(deleteResult.IsSuccess);

        // Lipitor is deactivated, NOT deleted
        var fetchedMed = await _medicineService.GetMedicineByIdAsync(medId);
        Assert.NotNull(fetchedMed);
        Assert.False(fetchedMed.IsActive);

        // Verify historical prescription item snapshot is completely intact
        var rx = await _prescriptionService.GetPrescriptionByIdAsync(finalizeResult.Value!.Id);
        Assert.NotNull(rx);
        Assert.Single(rx.Items);
        var rxItem = rx.Items[0];
        Assert.Equal(medId, rxItem.MedicineId);
        Assert.Equal("Lipitor", rxItem.MedicineName);
        Assert.Equal("Atorvastatin", rxItem.GenericName);
        Assert.Equal("Tablet", rxItem.Form);
        Assert.Equal("20 mg", rxItem.Strength);
        Assert.Equal("1 tab", rxItem.Dose);
    }

    [Fact]
    public async Task MedicineService_Purge_WhenReferencedInPrescription_FailsWithExplicitWarning()
    {
        // Arrange
        var doctor = await SeedDoctorAsync();
        var patient = await SeedPatientAsync();

        var med = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Nexum",
            Form = "Capsule",
            Strength = "40 mg"
        });
        int medId = med.Value!.Id;

        await _prescriptionService.FinalizePrescriptionAsync(new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            Items =
            [
                new CreatePrescriptionMedicineDto
                {
                    MedicineId = medId,
                    MedicineName = "Nexum",
                    Form = "Capsule",
                    Strength = "40 mg",
                    Dose = "1 cap",
                    Frequency = "Before breakfast",
                    Route = "Oral"
                }
            ]
        });

        // Act - Attempt hard purge
        var purgeResult = await _medicineService.PurgeMedicineAsync(medId);

        // Assert
        Assert.False(purgeResult.IsSuccess);
        Assert.Contains("referenced by 1 historical prescription", purgeResult.ErrorMessage);
        Assert.Contains("deactivate it instead", purgeResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MedicineService_Purge_WhenNeverReferenced_SucceedsAndRemovesRow()
    {
        // Arrange
        var med = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "AccidentalEntry",
            Form = "Tablet",
            Strength = "5 mg"
        });
        int medId = med.Value!.Id;

        // Act - Purge unreferenced medicine
        var purgeResult = await _medicineService.PurgeMedicineAsync(medId);

        // Assert
        Assert.True(purgeResult.IsSuccess);
        var fetched = await _medicineService.GetMedicineByIdAsync(medId);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task MedicineService_UsageSummary_ReportsAccurateReferenceCounts()
    {
        // Arrange
        var doctor = await SeedDoctorAsync();
        var patient = await SeedPatientAsync();

        var med = await _medicineService.CreateMedicineAsync(new CreateMedicineDto { Name = "Cravit", Form = "Tablet", Strength = "500 mg" });
        int medId = med.Value!.Id;

        // Before prescribing
        var summaryBefore = await _medicineService.GetMedicineUsageSummaryAsync(medId);
        Assert.False(summaryBefore.IsReferencedInPrescriptions);
        Assert.Equal(0, summaryBefore.PrescriptionReferenceCount);
        Assert.True(summaryBefore.CanHardDelete);

        // Prescribe twice
        for (int i = 0; i < 2; i++)
        {
            await _prescriptionService.FinalizePrescriptionAsync(new CreatePrescriptionDto
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Items =
                [
                    new CreatePrescriptionMedicineDto
                    {
                        MedicineId = medId,
                        MedicineName = "Cravit",
                        Form = "Tablet",
                        Strength = "500 mg",
                        Dose = "1 tab",
                        Frequency = "Once daily",
                        Route = "Oral"
                    }
                ]
            });
        }

        // After prescribing
        var summaryAfter = await _medicineService.GetMedicineUsageSummaryAsync(medId);
        Assert.True(summaryAfter.IsReferencedInPrescriptions);
        Assert.Equal(2, summaryAfter.PrescriptionReferenceCount);
        Assert.False(summaryAfter.CanHardDelete);
    }

    [Fact]
    public async Task CatalogEdit_AfterPrescriptionFinalized_PreservesPrescriptionSnapshotImmutability()
    {
        // Arrange
        var doctor = await SeedDoctorAsync();
        var patient = await SeedPatientAsync();

        var med = await _medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "OriginalBrand",
            GenericName = "OriginalGeneric",
            Form = "Tablet",
            Strength = "100 mg"
        });
        int medId = med.Value!.Id;

        var rxResult = await _prescriptionService.FinalizePrescriptionAsync(new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            Items =
            [
                new CreatePrescriptionMedicineDto
                {
                    MedicineId = medId,
                    MedicineName = "OriginalBrand",
                    GenericName = "OriginalGeneric",
                    Form = "Tablet",
                    Strength = "100 mg",
                    Dose = "1 tab",
                    Frequency = "OD",
                    Route = "Oral"
                }
            ]
        });
        Assert.True(rxResult.IsSuccess);

        // Act - Edit the catalog entry formulation
        var updateResult = await _medicineService.UpdateMedicineAsync(new UpdateMedicineDto
        {
            Id = medId,
            Name = "RenamedBrand",
            GenericName = "RenamedGeneric",
            Form = "Capsule",
            Strength = "200 mg",
            IsActive = true
        }, allowDuplicate: true);
        Assert.True(updateResult.IsSuccess);

        // Assert - Verify the historical prescription item snapshot retains original values
        var historicalRx = await _prescriptionService.GetPrescriptionByIdAsync(rxResult.Value!.Id);
        Assert.NotNull(historicalRx);
        var historicalItem = historicalRx.Items[0];
        Assert.Equal("OriginalBrand", historicalItem.MedicineName);
        Assert.Equal("OriginalGeneric", historicalItem.GenericName);
        Assert.Equal("Tablet", historicalItem.Form);
        Assert.Equal("100 mg", historicalItem.Strength);
    }
}
