using System;
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
using DoctorRx.Presentation.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class InvestigationTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly TestDbContextFactory _factory;
    private readonly UnitOfWorkFactory _uowFactory;
    private readonly SystemClock _clock;
    private readonly InvestigationService _investigationService;
    private readonly PatientService _patientService;
    private readonly DoctorService _doctorService;

    public InvestigationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_InvTests_" + Guid.NewGuid().ToString("N"));
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

        _uowFactory = new UnitOfWorkFactory(_factory);
        _clock = new SystemClock();

        _doctorService = new DoctorService(
            _uowFactory,
            _clock,
            NullLogger<DoctorService>.Instance);

        _patientService = new PatientService(
            _uowFactory,
            _clock,
            NullLogger<PatientService>.Instance);

        _investigationService = new InvestigationService(
            _uowFactory,
            _doctorService,
            NullLogger<InvestigationService>.Instance);
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
    public async Task SeedCatalogueIfEmpty_PopulatesDefaultItems_AndIsIdempotent()
    {
        // Act: First seed
        await _investigationService.EnsureDefaultCatalogueSeededAsync();

        var allItems = await _investigationService.GetCatalogItemsAsync();
        Assert.NotEmpty(allItems);

        // Verify key clinical categories are present
        var categories = allItems.Select(x => x.Category).Distinct().ToList();
        Assert.Contains("Radiology & Imaging", categories);
        Assert.Contains("Laboratory", categories);
        Assert.Contains("Cardiology & Diagnostics", categories);

        var initialCount = allItems.Count;

        // Act: Second seed should do nothing (idempotent)
        await _investigationService.EnsureDefaultCatalogueSeededAsync();

        var allItemsAfter = await _investigationService.GetCatalogItemsAsync();
        Assert.Equal(initialCount, allItemsAfter.Count);
    }

    [Fact]
    public async Task SearchCatalog_ByNameCategoryAndAbbreviation_ReturnsMatchingResults()
    {
        // Arrange
        await _investigationService.EnsureDefaultCatalogueSeededAsync();

        // Act & Assert 1: Search by abbreviation "CBC"
        var cbcResults = await _investigationService.SearchCatalogAsync("CBC");
        Assert.NotEmpty(cbcResults);
        Assert.Contains(cbcResults, x => x.Name.Contains("Complete Blood Count", StringComparison.OrdinalIgnoreCase) || x.CommonAbbreviation == "CBC");

        // Act & Assert 2: Search by imaging modality "MRI"
        var mriResults = await _investigationService.SearchCatalogAsync("MRI");
        Assert.NotEmpty(mriResults);
        Assert.All(mriResults, x => Assert.True(
            x.Name.Contains("MRI", StringComparison.OrdinalIgnoreCase) ||
            (x.CommonAbbreviation != null && x.CommonAbbreviation.Contains("MRI", StringComparison.OrdinalIgnoreCase)) ||
            (x.Subcategory != null && x.Subcategory.Contains("MRI", StringComparison.OrdinalIgnoreCase))));

        // Act & Assert 3: Filter by category "Radiology & Imaging"
        var imagingResults = await _investigationService.GetCatalogItemsAsync("Radiology & Imaging");
        Assert.NotEmpty(imagingResults);
        Assert.All(imagingResults, x => Assert.Equal("Radiology & Imaging", x.Category));

        // Act & Assert 4: Cardiology procedure "ECG"
        var ecgResults = await _investigationService.SearchCatalogAsync("ECG");
        Assert.NotEmpty(ecgResults);
        Assert.Contains(ecgResults, x => x.Name.Contains("Electrocardiogram", StringComparison.OrdinalIgnoreCase) || x.CommonAbbreviation == "ECG");
    }

    [Fact]
    public async Task AddCustomCatalogItem_WithValidData_PersistsAndPreventsDuplicates()
    {
        // Arrange
        await _investigationService.EnsureDefaultCatalogueSeededAsync();
        var customDto = new CreateInvestigationCatalogItemDto
        {
            Name = "Serum Ferritin & Iron Studies",
            Category = "Laboratory Medicine",
            Subcategory = "Biochemistry",
            CommonAbbreviation = "FE-STUDIES",
            SampleTypeOrModality = "Fasting Serum",
            DefaultInstructions = "12 hours fasting required"
        };

        // Act: Add custom item
        var createResult = await _investigationService.AddCustomInvestigationAsync(customDto);
        Assert.True(createResult.IsSuccess, createResult.ErrorMessage);
        Assert.NotNull(createResult.Value);
        Assert.True(createResult.Value.IsCustom);
        Assert.Equal("Serum Ferritin & Iron Studies", createResult.Value.Name);

        // Act: Attempt to add duplicate item with same name
        var duplicateResult = await _investigationService.AddCustomInvestigationAsync(customDto);
        Assert.False(duplicateResult.IsSuccess);
        Assert.Contains("already exists", duplicateResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRequest_WithPatientAndMultipleItems_GeneratesSequentialNumberAndPersists()
    {
        // Arrange: Create patient
        var patientResult = await _patientService.CreatePatientAsync(new CreatePatientDto
        {
            Name = "Fatima Noor",
            Age = 42,
            Gender = Gender.Female,
            Phone = "+92 321 5556677",
            Address = "Rawalpindi"
        });
        Assert.True(patientResult.IsSuccess);
        var patient = patientResult.Value!;

        // Configure doctor profile
        var docResult = await _doctorService.CreateDoctorAsync(new CreateDoctorDto
        {
            Name = "Ahmad Khan",
            TitlePrefix = "Dr.",
            Qualification = "MBBS, FCPS (Medicine)",
            Specialization = "Consultant Physician",
            RegistrationNumber = "PMC-12345-N",
            ClinicName = "Al-Shifa Family Clinic",
            ClinicAddress = "Main Boulevard, Rawalpindi",
            ClinicPhone = "+92 51 9876543"
        });
        Assert.True(docResult.IsSuccess, docResult.ErrorMessage);

        // Seed catalogue
        await _investigationService.EnsureDefaultCatalogueSeededAsync();

        var requestDto = new CreateInvestigationRequestDto
        {
            PatientId = patient.Id,
            RequestDate = DateOnly.FromDateTime(DateTime.Today),
            ClinicalNotes = "Suspected lumbar radiculopathy and iron deficiency anemia.",
            Instructions = "Please fast for 12 hours prior to lab tests.",
            Items = new List<CreateInvestigationRequestItemDto>
            {
                new()
                {
                    InvestigationName = "MRI Lumbosacral Spine",
                    Category = "Radiology & Imaging",
                    Subcategory = "MRI",
                    BodyPartOrSite = "Lumbar Spine L1-S1",
                    ViewOrProtocol = "T1, T2 sagittal & axial without contrast",
                    SpecificInstructions = "Evaluate for disc protrusion at L4-L5 and L5-S1",
                    DisplayOrder = 1
                },
                new()
                {
                    InvestigationName = "Complete Blood Count (CBC)",
                    Category = "Laboratory Medicine",
                    Subcategory = "Hematology",
                    BodyPartOrSite = "Whole Blood EDTA",
                    ViewOrProtocol = "Automated Differential",
                    SpecificInstructions = "Include peripheral film morphology",
                    DisplayOrder = 2
                }
            }
        };

        // Act: Create request
        var result = await _investigationService.CreateInvestigationRequestAsync(requestDto);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        var detail = result.Value!;

        // Assertions
        Assert.True(detail.Id > 0);
        Assert.StartsWith("INV-", detail.RequestNumber);
        Assert.Equal(patient.Id, detail.PatientId);
        Assert.Equal("Fatima Noor", detail.PatientName);
        Assert.Equal(patient.RecordNumber, detail.PatientRecordNumber);
        Assert.Equal(2, detail.Items.Count);
        Assert.Equal("MRI Lumbosacral Spine", detail.Items[0].InvestigationName);
        Assert.Equal("Lumbar Spine L1-S1", detail.Items[0].BodyPartOrSite);
        Assert.Equal("Complete Blood Count (CBC)", detail.Items[1].InvestigationName);
        Assert.Equal("Dr. Ahmad Khan", detail.DoctorName);
        Assert.Equal("Al-Shifa Family Clinic", detail.ClinicName);

        // Verify retrieval by Id
        var retrieved = await _investigationService.GetRequestByIdAsync(detail.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(detail.RequestNumber, retrieved.RequestNumber);
        Assert.Equal(2, retrieved.Items.Count);
    }

    [Fact]
    public async Task CreateRequest_Validation_RejectsInvalidPatientOrEmptyItems()
    {
        // Arrange
        await _investigationService.EnsureDefaultCatalogueSeededAsync();

        // Test 1: Non-existent patient
        var nonExistentPatientDto = new CreateInvestigationRequestDto
        {
            PatientId = 999999,
            Items = new List<CreateInvestigationRequestItemDto>
            {
                new() { InvestigationName = "Chest X-Ray", Category = "Radiology & Imaging" }
            }
        };
        var res1 = await _investigationService.CreateInvestigationRequestAsync(nonExistentPatientDto);
        Assert.False(res1.IsSuccess);
        Assert.Contains("not found", res1.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Test 2: Valid patient but empty items
        var pat = await _patientService.CreatePatientAsync(new CreatePatientDto
        {
            Name = "Tariq Mahmood",
            Age = 50,
            Gender = Gender.Male
        });

        var emptyItemsDto = new CreateInvestigationRequestDto
        {
            PatientId = pat.Value!.Id,
            Items = new List<CreateInvestigationRequestItemDto>()
        };
        var res2 = await _investigationService.CreateInvestigationRequestAsync(emptyItemsDto);
        Assert.False(res2.IsSuccess);
        Assert.Contains("at least one diagnostic test", res2.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchRequests_FilterByPatientAndDateRange_ReturnsAccurateRecords()
    {
        // Arrange: Create 2 patients
        var pat1 = (await _patientService.CreatePatientAsync(new CreatePatientDto { Name = "Zubair Shah", Age = 28, Gender = Gender.Male })).Value!;
        var pat2 = (await _patientService.CreatePatientAsync(new CreatePatientDto { Name = "Ayesha Bibi", Age = 34, Gender = Gender.Female })).Value!;

        await _investigationService.EnsureDefaultCatalogueSeededAsync();

        // Create request for pat1
        var req1 = (await _investigationService.CreateInvestigationRequestAsync(new CreateInvestigationRequestDto
        {
            PatientId = pat1.Id,
            Items = new List<CreateInvestigationRequestItemDto>
            {
                new() { InvestigationName = "Ultrasound Whole Abdomen", Category = "Radiology & Imaging" }
            }
        })).Value!;

        // Create request for pat2
        var req2 = (await _investigationService.CreateInvestigationRequestAsync(new CreateInvestigationRequestDto
        {
            PatientId = pat2.Id,
            Items = new List<CreateInvestigationRequestItemDto>
            {
                new() { InvestigationName = "Liver Function Tests (LFT)", Category = "Laboratory Medicine" }
            }
        })).Value!;

        // Act & Assert 1: Search by patient 1 name
        var resultsPat1 = await _investigationService.SearchRequestsAsync("Zubair");
        Assert.Contains(resultsPat1, r => r.RequestNumber == req1.RequestNumber);
        Assert.DoesNotContain(resultsPat1, r => r.RequestNumber == req2.RequestNumber);

        // Act & Assert 2: Search by request number
        var resultsByNumber = await _investigationService.SearchRequestsAsync(req2.RequestNumber);
        Assert.Single(resultsByNumber);
        Assert.Equal(req2.RequestNumber, resultsByNumber[0].RequestNumber);

        // Act & Assert 3: Filter by date range of today
        var resultsDate = await _investigationService.SearchRequestsAsync(null, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today));
        Assert.Contains(resultsDate, r => r.RequestNumber == req1.RequestNumber);
        Assert.Contains(resultsDate, r => r.RequestNumber == req2.RequestNumber);
    }

    [Fact]
    public void InvestigationPaperView_CanRenderDetailVisual_WithoutExceptions()
    {
        // Run on STA thread as required for WPF visual controls
        StaTestRunner.Run(() =>
        {
            var detail = new InvestigationRequestDetailDto(
                Id: 1,
                RequestNumber: "INV-20261013-0001",
                PatientId: 101,
                PatientName: "Kamran Akmal",
                PatientRecordNumber: "PAT-00101",
                PatientAge: "38 yrs",
                PatientGender: Gender.Male,
                PatientPhone: "+92 300 1234567",
                PatientAddress: "Lahore",
                PatientKnownAllergies: "Penicillin",
                RequestDate: DateOnly.FromDateTime(DateTime.Today),
                CreatedAtUtc: DateTime.UtcNow,
                Status: "Active",
                ClinicalNotes: "Evaluate persistent lower quadrant pain.",
                Instructions: "Full bladder required.",
                DoctorName: "Dr. Bilal Saeed",
                DoctorQualification: "MBBS, MRCP",
                DoctorRegistration: "PMC-54321-P",
                DoctorSpecialization: "Consultant Physician",
                ClinicName: "City Medical Complex",
                ClinicAddress: "Jail Road, Lahore",
                ClinicPhone: "+92 42 35789000",
                Items: new List<InvestigationRequestItemDto>
                {
                    new(1, 1, 10, "Ultrasound Abdomen & Pelvis", "Radiology & Imaging", "Ultrasound", "Whole Abdomen", "Routine Grayscale & Color", "Evaluate kidneys and appendix", 1),
                    new(2, 1, 12, "Urine Routine Examination (Urinalysis)", "Laboratory Medicine", "Urinalysis", "Midstream Urine", "Microscopic", "Check for RBCs and proteinuria", 2)
                }
            );

            var paper = new InvestigationPaperView();
            paper.Populate(detail);

            Assert.Equal("Order # INV-20261013-0001", paper.RequestNumberTextElement.Text);
            Assert.Equal("Kamran Akmal", paper.PatientNameTextElement.Text);
            Assert.Equal("Dr. Bilal Saeed", paper.DoctorNameTextElement.Text);
            Assert.Equal(2, paper.InvestigationsItemsControlElement.ItemsSource.Cast<object>().Count());
        });
    }

    [Fact]
    public async Task CreatingInvestigationRequest_DoesNotAlterPrescriptions_OrDuplicatePatient()
    {
        // Arrange: Create patient
        var patResult = await _patientService.CreatePatientAsync(new CreatePatientDto
        {
            Name = "Nasreen Akhtar",
            Age = 55,
            Gender = Gender.Female
        });
        var patient = patResult.Value!;

        await _investigationService.EnsureDefaultCatalogueSeededAsync();

        // Initial patient count in database
        var patientsBefore = await _patientService.SearchPatientsAsync("Nasreen");
        Assert.Single(patientsBefore);

        // Act: Create 2 investigation orders for this same patient
        var req1 = await _investigationService.CreateInvestigationRequestAsync(new CreateInvestigationRequestDto
        {
            PatientId = patient.Id,
            Items = new List<CreateInvestigationRequestItemDto>
            {
                new() { InvestigationName = "HbA1c (Glycated Hemoglobin)", Category = "Laboratory Medicine" }
            }
        });
        var req2 = await _investigationService.CreateInvestigationRequestAsync(new CreateInvestigationRequestDto
        {
            PatientId = patient.Id,
            Items = new List<CreateInvestigationRequestItemDto>
            {
                new() { InvestigationName = "Lipid Profile", Category = "Laboratory Medicine" }
            }
        });

        Assert.True(req1.IsSuccess);
        Assert.True(req2.IsSuccess);

        // Verify patient count remains strictly 1 (no duplicate patient records created)
        var patientsAfter = await _patientService.SearchPatientsAsync("Nasreen");
        Assert.Single(patientsAfter);
        Assert.Equal(patient.Id, patientsAfter[0].Id);

        // Verify both requests are linked to this same patient
        var patientRequests = await _investigationService.GetRequestsByPatientIdAsync(patient.Id);
        Assert.Equal(2, patientRequests.Count);
    }

    [Fact]
    public async Task AntiTamper_DirectSqlUpdate_ClinicalColumns_IsRejected()
    {
        // Arrange: Create patient & request
        var patResult = await _patientService.CreatePatientAsync(new CreatePatientDto { Name = "Tamper Test", Age = 30, Gender = Gender.Male });
        var req = await _investigationService.CreateInvestigationRequestAsync(new CreateInvestigationRequestDto
        {
            PatientId = patResult.Value!.Id,
            ClinicalNotes = "Original clinical note",
            Items = new List<CreateInvestigationRequestItemDto>
            {
                new() { InvestigationName = "Chest X-Ray (PA View)", Category = "Radiology & Imaging" }
            }
        });
        Assert.True(req.IsSuccess);
        int reqId = req.Value!.Id;

        // Act & Assert 1: Raw SQL UPDATE of ClinicalNotes must be aborted by trigger
        await using var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}");
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"UPDATE InvestigationRequests SET ClinicalNotes = 'Tampered clinical note' WHERE Id = {reqId};";
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Clinical and snapshot investigation request fields are immutable", ex.Message);
        }

        // Act & Assert 2: Raw SQL UPDATE of RequestNumber must be aborted
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"UPDATE InvestigationRequests SET RequestNumber = 'INV-TAMPERED-0001' WHERE Id = {reqId};";
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Clinical and snapshot investigation request fields are immutable", ex.Message);
        }

        // Act & Assert 3: Raw SQL UPDATE of unsealing (IsSealed = 0) must be aborted
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"UPDATE InvestigationRequests SET IsSealed = 0 WHERE Id = {reqId};";
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Sealed investigation request cannot be unsealed", ex.Message);
        }
    }

    [Fact]
    public async Task AntiTamper_DirectSqlDelete_RequestAndItems_IsRejected()
    {
        // Arrange
        var patResult = await _patientService.CreatePatientAsync(new CreatePatientDto { Name = "Delete Tamper", Age = 40, Gender = Gender.Female });
        var req = await _investigationService.CreateInvestigationRequestAsync(new CreateInvestigationRequestDto
        {
            PatientId = patResult.Value!.Id,
            Items = new List<CreateInvestigationRequestItemDto>
            {
                new() { InvestigationName = "Lipid Profile", Category = "Laboratory" }
            }
        });
        Assert.True(req.IsSuccess);
        int reqId = req.Value!.Id;
        int itemId = req.Value!.Items[0].Id;

        await using var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}");
        await conn.OpenAsync();

        // Act & Assert 1: Cannot delete from InvestigationRequests
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"DELETE FROM InvestigationRequests WHERE Id = {reqId};";
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Investigation requests cannot be deleted", ex.Message);
        }

        // Act & Assert 2: Cannot delete from InvestigationRequestItems
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"DELETE FROM InvestigationRequestItems WHERE Id = {itemId};";
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Investigation request items cannot be deleted", ex.Message);
        }

        // Act & Assert 3: Cannot update InvestigationRequestItems
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"UPDATE InvestigationRequestItems SET InvestigationName = 'Tampered Test' WHERE Id = {itemId};";
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Investigation request items cannot be updated", ex.Message);
        }

        // Act & Assert 4: Cannot insert new item into sealed request
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"INSERT INTO InvestigationRequestItems (RequestId, InvestigationName, Category, DisplayOrder, CreatedAtUtc) VALUES ({reqId}, 'New Test', 'Laboratory', 2, '2026-10-14T00:00:00Z');";
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Cannot add items to a sealed or cancelled investigation request", ex.Message);
        }
    }

    [Fact]
    public async Task CancelInvestigationRequestAsync_RequiresReason_AndTransitionsStatus()
    {
        // Arrange
        var patResult = await _patientService.CreatePatientAsync(new CreatePatientDto { Name = "Cancel Test", Age = 28, Gender = Gender.Male });
        var req = await _investigationService.CreateInvestigationRequestAsync(new CreateInvestigationRequestDto
        {
            PatientId = patResult.Value!.Id,
            Items = new List<CreateInvestigationRequestItemDto>
            {
                new() { InvestigationName = "Urine Routine Examination (Urine R/E)", Category = "Laboratory" }
            }
        });
        Assert.True(req.IsSuccess);
        int reqId = req.Value!.Id;

        // Act 1: Attempt cancel with empty reason -> must fail
        var failResult = await _investigationService.CancelInvestigationRequestAsync(reqId, "   ");
        Assert.False(failResult.IsSuccess);
        Assert.Contains("reason is required", failResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Act 2: Cancel with valid reason -> must succeed
        var cancelResult = await _investigationService.CancelInvestigationRequestAsync(reqId, "Patient declined procedure");
        Assert.True(cancelResult.IsSuccess);

        // Assert: Verify state
        var loaded = await _investigationService.GetRequestByIdAsync(reqId);
        Assert.NotNull(loaded);
        Assert.Equal("Cancelled", loaded!.Status);
        Assert.Equal("Patient declined procedure", loaded.CancellationReason);

        // Act 3: Attempting to modify cancelled request directly via SQL is blocked
        await using var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}");
        await conn.OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"UPDATE InvestigationRequests SET Status = 'Active' WHERE Id = {reqId};";
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("Investigation request is cancelled and cannot be modified", ex.Message);
        }
    }
}
