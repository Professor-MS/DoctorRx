using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
using DoctorRx.Presentation.Behaviors;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class NewPrescriptionViewModelTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestDbContextFactory _factory;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;
    private readonly IPatientService _patientService;
    private readonly IMedicineService _medicineService;
    private readonly IPrescriptionService _prescriptionService;
    private readonly IDraftService _draftService;
    private readonly IPrescriptionComposerValidator _validator;
    private readonly TestDialogService _dialogService;
    private readonly TestNavigationService _navigationService;
    private readonly TestQuickPhrasesService _quickPhrasesService;

    public NewPrescriptionViewModelTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_VMTests_" + Guid.NewGuid().ToString("N"));
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

        using (var db = _factory.CreateDbContext())
        {
            db.Database.Migrate();

            // Seed active doctor
            db.Doctors.Add(new Doctor
            {
                Name = "Dr. Asim Khan",
                Qualification = "MBBS, FCPS",
                ClinicName = "Al-Shifa Clinic",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        _draftService = new DraftService(_uowFactory, _clock, NullLogger<DraftService>.Instance);
        _prescriptionService = new PrescriptionService(_uowFactory, _clock, NullLogger<PrescriptionService>.Instance, _draftService);
        _patientService = new PatientService(_uowFactory, _clock, NullLogger<PatientService>.Instance);
        _medicineService = new MedicineService(_uowFactory, _clock, NullLogger<MedicineService>.Instance);
        _validator = new PrescriptionComposerValidator(_clock);
        _dialogService = new TestDialogService();
        _navigationService = new TestNavigationService();
        _quickPhrasesService = new TestQuickPhrasesService();
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
        catch { }
    }

    private NewPrescriptionViewModel CreateViewModel(IQuickPhrasesService? quickPhrasesService = null)
    {
        return new NewPrescriptionViewModel(
            _patientService,
            _medicineService,
            _prescriptionService,
            _draftService,
            _dialogService,
            _navigationService,
            _validator,
            _clock,
            quickPhrasesService ?? _quickPhrasesService);
    }

    [Fact]
    public void SelectCatalogMedicine_NeverFillsClinicalDefaults_ZeroDefaultingPrinciple()
    {
        // Arrange
        var vm = CreateViewModel();
        var catalogMed = new MedicineDto(
            Id: 42,
            Name: "Amoxicillin",
            GenericName: "Amoxicillin Trihydrate",
            Form: "Capsule",
            Strength: "500 mg",
            IsActive: true);

        // Act
        vm.SelectCatalogMedicineCommand.Execute(catalogMed);

        // Assert - Identity fields prefilled
        Assert.Equal("Amoxicillin", vm.MedicineName);
        Assert.Equal("Amoxicillin Trihydrate", vm.GenericName);
        Assert.Equal("Capsule", vm.Form);
        Assert.Equal("500 mg", vm.Strength);

        // Assert - Clinical decision fields MUST remain blank / default AsDirected (NEVER defaulted)
        Assert.True(string.IsNullOrEmpty(vm.Dose), "Dose must not be pre-filled!");
        Assert.True(string.IsNullOrEmpty(vm.Frequency), "Frequency must not be pre-filled!");
        Assert.True(string.IsNullOrEmpty(vm.Duration), "Duration must not be pre-filled!");
        Assert.True(string.IsNullOrEmpty(vm.Route), "Route must not be pre-filled!");
        Assert.Equal(MealRelation.AsDirected, vm.MealRelation);
    }

    [Fact]
    public void AddMedicine_WithValidInputs_AddsToPrescriptionList()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.MedicineName = "Panadol";
        vm.Form = "Tablet";
        vm.Strength = "500 mg";
        vm.Dose = "1 tablet";
        vm.Frequency = "TDS";
        vm.Duration = "5 days";
        vm.MealRelation = MealRelation.AfterMeal;

        // Act
        vm.AddOrUpdateMedicineCommand.Execute(null);

        // Assert
        Assert.Single(vm.PrescribedMedicines);
        var row = vm.PrescribedMedicines[0];
        Assert.Equal("Panadol", row.MedicineName);
        Assert.Equal("1 tablet", row.Dose);
        Assert.Equal("TDS", row.Frequency);
        Assert.Equal("5 days", row.Duration);
        Assert.Equal(MealRelation.AfterMeal, row.MealRelation);
        Assert.False(vm.HasValidationError);

        // Editor is reset
        Assert.True(string.IsNullOrEmpty(vm.MedicineName));
        Assert.True(string.IsNullOrEmpty(vm.Dose));
    }

    [Fact]
    public void AddMedicine_WithOmittedForm_IssuesWarningButAddsMedicine_Amendment4()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.MedicineName = "Syp Brufen";
        vm.Form = string.Empty; // Form omitted
        vm.Dose = "5 ml";
        vm.Frequency = "BD";
        vm.Duration = "3 days";

        // Act
        vm.AddOrUpdateMedicineCommand.Execute(null);

        // Assert - Row was successfully added
        Assert.Single(vm.PrescribedMedicines);
        Assert.False(vm.HasValidationError);
        Assert.True(vm.HasValidationWarning, "Omitted form should trigger a non-blocking warning");
        Assert.Contains("form", vm.ValidationWarningMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddMedicine_WithMissingRequiredFields_SetsValidationErrorAndDoesNotAdd()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.MedicineName = "Paracetamol";
        vm.Dose = string.Empty; // Missing required dose
        vm.Frequency = "BD";

        // Act
        vm.AddOrUpdateMedicineCommand.Execute(null);

        // Assert
        Assert.Empty(vm.PrescribedMedicines);
        Assert.True(vm.HasValidationError);
        Assert.Contains("Dose is required", vm.ValidationErrorMessage);
    }

    [Fact]
    public void DeleteMedicine_EnablesUndoBuffer_AndUndoRestoresItemAtOriginalIndex()
    {
        // Arrange
        var vm = CreateViewModel();

        vm.MedicineName = "Medicine A";
        vm.Dose = "1 tab";
        vm.Frequency = "OD";
        vm.AddOrUpdateMedicineCommand.Execute(null);

        vm.MedicineName = "Medicine B";
        vm.Dose = "2 tabs";
        vm.Frequency = "BD";
        vm.AddOrUpdateMedicineCommand.Execute(null);

        Assert.Equal(2, vm.PrescribedMedicines.Count);
        var firstItem = vm.PrescribedMedicines[0];

        // Act - Delete first item
        vm.DeleteMedicineRowCommand.Execute(firstItem);

        // Assert - Item deleted, undo available
        Assert.Single(vm.PrescribedMedicines);
        Assert.Equal("Medicine B", vm.PrescribedMedicines[0].MedicineName);
        Assert.True(vm.IsUndoAvailable);

        // Act - Undo
        vm.UndoDeleteMedicineCommand.Execute(null);

        // Assert - Item restored to index 0
        Assert.Equal(2, vm.PrescribedMedicines.Count);
        Assert.Equal("Medicine A", vm.PrescribedMedicines[0].MedicineName);
        Assert.False(vm.IsUndoAvailable);
    }

    [Fact]
    public void MoveUpAndMoveDown_CorrectlyReordersMedicines()
    {
        // Arrange
        var vm = CreateViewModel();

        vm.MedicineName = "Med 1";
        vm.Dose = "1 tab";
        vm.Frequency = "OD";
        vm.AddOrUpdateMedicineCommand.Execute(null);

        vm.MedicineName = "Med 2";
        vm.Dose = "1 tab";
        vm.Frequency = "BD";
        vm.AddOrUpdateMedicineCommand.Execute(null);

        var med1 = vm.PrescribedMedicines[0];
        var med2 = vm.PrescribedMedicines[1];

        // Act - Move Med 1 down
        vm.MoveDownMedicineRowCommand.Execute(med1);

        // Assert
        Assert.Equal("Med 2", vm.PrescribedMedicines[0].MedicineName);
        Assert.Equal("Med 1", vm.PrescribedMedicines[1].MedicineName);

        // Act - Move Med 1 back up
        vm.MoveUpMedicineRowCommand.Execute(med1);

        // Assert
        Assert.Equal("Med 1", vm.PrescribedMedicines[0].MedicineName);
        Assert.Equal("Med 2", vm.PrescribedMedicines[1].MedicineName);
    }

    private static List<string> GetBuiltInChipParametersFromXaml()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "DoctorRx.sln")))
        {
            currentDir = currentDir.Parent;
        }
        var projectRoot = currentDir?.FullName ?? AppContext.BaseDirectory;
        var xamlPath = Path.Combine(projectRoot, "src", "DoctorRx.Presentation", "Views", "NewPrescriptionView.xaml");
        var doc = System.Xml.Linq.XDocument.Parse(File.ReadAllText(xamlPath));
        return doc.Descendants()
            .Where(e => e.Name.LocalName == "Button" &&
                        e.Attribute("Command")?.Value.Contains("ChipCommand") == true &&
                        e.Attribute("CommandParameter") != null)
            .Select(e => e.Attribute("CommandParameter")!.Value)
            .Distinct()
            .ToList();
    }

    [Fact]
    public void QuickChips_Blocklist_NeverInsertsForbiddenAbbreviations()
    {
        // Whole-word case-insensitive matching on forbidden abbreviations:
        // OD, OS, OU, BD, TDS, QID, SOS, PRN, SC, IM, IV, Stat, tab, cap, syp, syrp, inj
        var forbiddenWords = new[]
        {
            "OD", "OS", "OU", "BD", "TDS", "QID", "SOS", "PRN", "SC", "IM", "IV", "Stat",
            "tab", "cap", "syp", "syrp", "inj"
        };

        var chips = GetBuiltInChipParametersFromXaml();
        Assert.NotEmpty(chips);

        foreach (var word in forbiddenWords)
        {
            var pattern = $@"\b{Regex.Escape(word)}\b";
            foreach (var chip in chips)
            {
                var match = Regex.IsMatch(chip, pattern, RegexOptions.IgnoreCase);
                Assert.False(match, $"Built-in chip '{chip}' contains forbidden abbreviation '{word}'");
            }
        }
    }

    [Fact]
    public void QuickChips_PatternTest_RejectsNumberWithAdministrationUnit_ExceptAllowedQuantities()
    {
        // Pattern rejecting a number followed by a volume, rate, drop or spray unit:
        // (mL, ml, mins, drops/min, drop, drops, spray)
        // Allowed unit quantities: 1 tablet, 2 tablets, 5 mL, 1 vial, 1 ampoule
        var allowedUnitQuantities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "1 tablet", "2 tablets", "5 mL", "1 vial", "1 ampoule"
        };

        var unitPattern = @"\d+\s*(mL|ml|mins|drops/min|drop|drops|spray)";
        var chips = GetBuiltInChipParametersFromXaml();
        Assert.NotEmpty(chips);

        foreach (var chip in chips)
        {
            if (Regex.IsMatch(chip, unitPattern, RegexOptions.IgnoreCase))
            {
                Assert.True(
                    allowedUnitQuantities.Contains(chip),
                    $"Built-in chip '{chip}' violates clinical safety rule: administration units/volumes/rates/counts are forbidden unless strictly in allowed unit quantities.");
            }
        }
    }

    [Fact]
    public void QuickChips_ClickingChip_ChangesOnlyTargetField_AndNeverOverwritesWithoutConfirmation()
    {
        // Arrange
        var vm = CreateViewModel();

        // 1. Initial click on empty field sets field directly without prompt
        vm.SelectFormChipCommand.Execute("Tablet");
        Assert.Equal("Tablet", vm.Form);
        Assert.Empty(vm.Dose);
        Assert.Empty(vm.Frequency);
        Assert.Empty(vm.Route);
        Assert.Empty(vm.Duration);

        // 2. Click with different value when field already contains text and user declines confirmation
        _dialogService.ConfirmationResult = false;
        vm.SelectFormChipCommand.Execute("Capsule");
        Assert.Equal("Tablet", vm.Form); // Preserved!

        // 3. Click when user accepts confirmation
        _dialogService.ConfirmationResult = true;
        vm.SelectFormChipCommand.Execute("Capsule");
        Assert.Equal("Capsule", vm.Form); // Updated after confirmation!

        // 4. Dose chip changes only Dose
        vm.SelectDoseChipCommand.Execute("1 tablet");
        Assert.Equal("1 tablet", vm.Dose);
        Assert.Equal("Capsule", vm.Form); // Other fields untouched

        // 5. Frequency chip changes only Frequency
        vm.SelectFrequencyChipCommand.Execute("Once daily");
        Assert.Equal("Once daily", vm.Frequency);

        // 6. Route chip changes only Route
        vm.SelectRouteChipCommand.Execute("Oral");
        Assert.Equal("Oral", vm.Route);

        // 7. Duration chip changes only Duration
        vm.SelectDurationChipCommand.Execute("5 days");
        Assert.Equal("5 days", vm.Duration);
    }

    [Fact]
    public async Task QuickPhrases_DoctorCustom_CrudAndPersistence()
    {
        // Arrange
        var testService = new TestQuickPhrasesService();
        var vm = CreateViewModel(testService);

        // Act 1: Add custom phrase (exempt from blocklist, doctor-owned)
        await vm.AddQuickPhraseAsync("Take 2 drops in left eye with warm water");
        await vm.AddQuickPhraseAsync("Dissolve in 200 ml juice");

        // Assert 1: Quick phrases populated and saved
        Assert.Equal(2, vm.QuickPhrases.Count);
        Assert.Equal("Take 2 drops in left eye with warm water", vm.QuickPhrases[0]);
        Assert.Equal("Dissolve in 200 ml juice", vm.QuickPhrases[1]);
        Assert.Equal(2, testService.Phrases.Count);

        // Act 2: Reorder phrases
        await vm.MoveDownQuickPhraseAsync("Take 2 drops in left eye with warm water");
        Assert.Equal("Dissolve in 200 ml juice", vm.QuickPhrases[0]);
        Assert.Equal("Take 2 drops in left eye with warm water", vm.QuickPhrases[1]);
        Assert.Equal("Dissolve in 200 ml juice", testService.Phrases[0]);

        // Act 3: Click phrase to insert into Special Instructions
        vm.SelectQuickPhraseCommand.Execute("Dissolve in 200 ml juice");
        Assert.Equal("Dissolve in 200 ml juice", vm.Instructions);

        // Act 4: Delete phrase
        await vm.DeleteQuickPhraseAsync("Dissolve in 200 ml juice");
        Assert.Single(vm.QuickPhrases);
        Assert.Equal("Take 2 drops in left eye with warm water", vm.QuickPhrases[0]);
        Assert.Single(testService.Phrases);
    }

    [Fact]
    public void AdvicePresets_AppendsCleanlyWithNewlines()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.AddAdvicePresetCommand.Execute("Take plenty of fluids.");
        vm.AddAdvicePresetCommand.Execute("Avoid oily food.");

        // Assert
        Assert.Contains("Take plenty of fluids.", vm.GeneralAdvice);
        Assert.Contains("Avoid oily food.", vm.GeneralAdvice);
        Assert.Contains("\n", vm.GeneralAdvice);
    }

    [Fact]
    public void AutoFlowDirection_CorrectlyDetectsUrduAndEnglish()
    {
        // Urdu text
        Assert.True(AutoFlowDirection.IsUrduOrArabic("پینا ہے بعد از غذا"));
        Assert.True(AutoFlowDirection.IsUrduOrArabic("کھانے کے بعد ۲ چمچ"));

        // English text
        Assert.False(AutoFlowDirection.IsUrduOrArabic("Take 1 tablet after meals"));
        Assert.False(AutoFlowDirection.IsUrduOrArabic("1 capsule TDS x 5 days"));
        Assert.False(AutoFlowDirection.IsUrduOrArabic(""));
        Assert.False(AutoFlowDirection.IsUrduOrArabic(null));
    }

    [Fact]
    public async Task FinalizePrescription_WithMissingPatient_ShowsErrorAndDoesNotFinalize()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.MedicineName = "Panadol";
        vm.Dose = "1 tab";
        vm.Frequency = "TDS";
        vm.AddOrUpdateMedicineCommand.Execute(null);

        // Act - Attempt to finalize without selected patient
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.FinalizePrescriptionCommand).ExecuteAsync(null);

        // Assert
        Assert.True(vm.HasValidationError);
        Assert.Contains("patient must be selected", vm.ValidationErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(_navigationService.LastNavigatedDestination);
    }

    [Fact]
    public async Task FinalizePrescription_ValidState_CallsFinalizeAndNavigatesToDetail()
    {
        // Arrange
        var createPatient = await _patientService.CreatePatientAsync(new CreatePatientDto
        {
            Name = "Muhammad Ali",
            Age = 35,
            Gender = Gender.Male,
            Phone = "03001234567"
        });
        Assert.True(createPatient.IsSuccess);

        var vm = CreateViewModel();
        vm.SelectPatientCommand.Execute(createPatient.Value);

        vm.MedicineName = "Amoxicillin";
        vm.Form = "Capsule";
        vm.Strength = "500 mg";
        vm.Dose = "1 capsule";
        vm.Frequency = "TDS";
        vm.Duration = "5 days";
        vm.AddOrUpdateMedicineCommand.Execute(null);

        // Act - Finalize
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.FinalizePrescriptionCommand).ExecuteAsync(null);

        // Assert
        Assert.Equal(NavigationDestination.PrescriptionDetail, _navigationService.LastNavigatedDestination);
        Assert.NotNull(_navigationService.LastNavigatedParameter);
        var detail = Assert.IsType<PrescriptionDetailDto>(_navigationService.LastNavigatedParameter);
        Assert.True(detail.Id > 0);

        // Verify draft was deleted on finalize
        var activeDrafts = await _draftService.ListAsync();
        Assert.Empty(activeDrafts);
    }

    [Fact]
    public async Task ZombieDraft_WhenFinalizeStarts_AutosaveRefusesToWriteAndNoDraftRemains_Amendment1()
    {
        // Arrange
        var createPatient = await _patientService.CreatePatientAsync(new CreatePatientDto
        {
            Name = "Fatima Bibi",
            Age = 40,
            Gender = Gender.Female
        });
        Assert.True(createPatient.IsSuccess);

        var vm = CreateViewModel();
        vm.SelectPatientCommand.Execute(createPatient.Value);

        vm.MedicineName = "Ciprofloxacin";
        vm.Form = "Tablet";
        vm.Strength = "500 mg";
        vm.Dose = "1 tab";
        vm.Frequency = "BD";
        vm.Duration = "5 days";
        vm.AddOrUpdateMedicineCommand.Execute(null);

        // Save a draft before finalizing
        await vm.SaveDraftInternalAsync(explicitUserSave: true);
        var preDrafts = await _draftService.ListAsync();
        Assert.Single(preDrafts);

        // Act 1 - Finalize prescription
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.FinalizePrescriptionCommand).ExecuteAsync(null);

        // Draft deleted upon finalize
        var postFinalizeDrafts = await _draftService.ListAsync();
        Assert.Empty(postFinalizeDrafts);

        // Act 2 - Simulate an autosave attempt firing during/after finalize
        await vm.SaveDraftInternalAsync(explicitUserSave: false);

        // Assert - Zombie draft race prevented: no draft resurrected in DB
        var finalDrafts = await _draftService.ListAsync();
        Assert.Empty(finalDrafts);
    }

    [Fact]
    public async Task DraftRecovery_RefreshesPatientFromDatabase_NeverTrustsPayloadCopies_Amendment3()
    {
        // Arrange
        var createPatient = await _patientService.CreatePatientAsync(new CreatePatientDto
        {
            Name = "Initial Patient Name",
            Age = 50,
            Gender = Gender.Male,
            Phone = "03009998877"
        });
        Assert.True(createPatient.IsSuccess);
        var patientId = createPatient.Value!.Id;

        var vm1 = CreateViewModel();
        vm1.SelectPatientCommand.Execute(createPatient.Value);
        vm1.MedicineName = "Aspirin";
        vm1.Dose = "75 mg";
        vm1.Frequency = "OD";
        vm1.AddOrUpdateMedicineCommand.Execute(null);
        await vm1.SaveDraftInternalAsync(explicitUserSave: true);
        var draftKey = vm1.DraftKey;

        // Simulate patient profile being edited / updated in DB after draft was created
        await using (var uow = _uowFactory.Create())
        {
            var p = await uow.Patients.GetByIdAsync(patientId);
            Assert.NotNull(p);
            p.Name = "Updated Legal Patient Name";
            p.Phone = "03110001122";
            await uow.Patients.UpdateAsync(p);
            await uow.CommitAsync();
        }

        // Act - Recover draft in a new composer session
        var vm2 = CreateViewModel();
        await vm2.InitializeAsync(draftKey);

        // Assert - Patient fields refreshed from live DB, not stale payload copies
        Assert.NotNull(vm2.SelectedPatient);
        Assert.Equal("Updated Legal Patient Name", vm2.SelectedPatient.Name);
        Assert.Equal("03110001122", vm2.SelectedPatient.Phone);
    }

    [Fact]
    public async Task DraftList_FlagsOlderThan30Days_NeverAutoDeletesDrafts_Amendment10()
    {
        // Arrange: manually insert a draft with UpdatedAtUtc 40 days ago
        var oldDraftKey = Guid.NewGuid();
        var oldDate = DateTime.UtcNow.AddDays(-40);

        await using (var uow = _uowFactory.Create())
        {
            var draft = new Draft
            {
                DraftKey = oldDraftKey,
                PayloadJson = "{\"DraftKey\":\"" + oldDraftKey + "\",\"SchemaVersion\":1,\"Items\":[]}",
                PayloadVersion = 1,
                CreatedAtUtc = oldDate,
                UpdatedAtUtc = oldDate,
                AppVersion = "1.0.0"
            };
            await uow.Drafts.AddAsync(draft);
            await uow.CommitAsync();
        }

        // Act
        var drafts = await _draftService.ListAsync();

        // Assert
        Assert.Single(drafts);
        var summary = drafts[0];
        Assert.Equal(oldDraftKey, summary.DraftKey);
        Assert.True(summary.IsOlderThan30Days, "Draft older than 30 days must be flagged");

        // Verify count remains 1 (never auto-deleted per Amendment 10)
        var count = await _draftService.GetCountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public void HasUnsavedChanges_CorrectlyReflectsEdits()
    {
        // Arrange & Act 1: Fresh VM
        var vm = CreateViewModel();
        Assert.False(vm.HasUnsavedChanges);

        // Act 2: Add patient
        vm.SelectPatientCommand.Execute(new PatientDto(
            Id: 1, RecordNumber: "MRN-001", Name: "Test", DateOfBirth: null, Age: 25,
            Gender: Gender.Male, Phone: null, Address: null, MedicalHistoryNotes: null,
            KnownAllergies: null, CreatedAtUtc: DateTime.UtcNow, LastVisitDate: null,
            IsArchived: false, ArchivedAtUtc: null));
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void AddMedicine_TypedFreeTextName_AddsRowSuccessfully()
    {
        var vm = CreateViewModel();
        string? focusedField = null;
        vm.FocusRequested += target => focusedField = target;

        // Simulating doctor typing free text "Panadol Mg"
        vm.MedicineSearchQuery = "Panadol Mg";
        vm.Form = "Tab";
        vm.Strength = "500 mg";
        vm.GenericName = "Paracetamol";
        vm.Dose = "1 tab";
        vm.Frequency = "Twice daily";
        vm.Duration = "5 days";
        vm.Route = "Oral";

        vm.AddOrUpdateMedicineCommand.Execute(null);

        Assert.Null(vm.ValidationErrorMessage);
        Assert.Null(vm.EditorErrorMessage);
        Assert.False(vm.HasEditorError);
        Assert.Single(vm.PrescribedMedicines);
        Assert.Equal("Panadol Mg", vm.PrescribedMedicines[0].MedicineName);
        Assert.Equal("MedicineName", focusedField); // Resets focus to MedicineName for the next medicine
        Assert.True(string.IsNullOrEmpty(vm.MedicineName));
    }

    [Fact]
    public void AddMedicine_SelectingSuggestion_AddsRowSuccessfully()
    {
        var vm = CreateViewModel();
        string? focusedField = null;
        vm.FocusRequested += target => focusedField = target;

        var catalogItem = new MedicineDto(
            Id: 42,
            Name: "Amoxicillin",
            GenericName: "Amoxicillin Trihydrate",
            Form: "Cap",
            Strength: "500 mg",
            IsActive: true);

        vm.SelectCatalogMedicineCommand.Execute(catalogItem);

        Assert.Equal("Amoxicillin", vm.MedicineName);
        Assert.Equal("Dose", focusedField); // Suggestion selection focuses Dose for doctor input

        vm.Dose = "1 cap";
        vm.Frequency = "Three times daily";
        vm.AddOrUpdateMedicineCommand.Execute(null);

        Assert.Null(vm.ValidationErrorMessage);
        Assert.Null(vm.EditorErrorMessage);
        Assert.False(vm.HasEditorError);
        Assert.Single(vm.PrescribedMedicines);
        Assert.Equal("Amoxicillin", vm.PrescribedMedicines[0].MedicineName);
        Assert.Equal(42, vm.PrescribedMedicines[0].MedicineId);
        Assert.Equal("MedicineName", focusedField);
    }

    [Fact]
    public void AddMedicine_MissingDoseOrFrequency_ShowsRightInlineError()
    {
        var vm = CreateViewModel();
        string? focusedField = null;
        vm.FocusRequested += target => focusedField = target;

        // 1. Missing Medicine Name
        vm.AddOrUpdateMedicineCommand.Execute(null);
        Assert.Equal("Medicine name is required.", vm.EditorErrorMessage);
        Assert.True(vm.HasEditorError);
        Assert.Equal("MedicineName", focusedField);

        // 2. Medicine Name provided, Missing Dose
        vm.MedicineName = "Panadol";
        vm.AddOrUpdateMedicineCommand.Execute(null);
        Assert.Equal("Dose is required (e.g. 1 tab, 5 ml, 1 drop).", vm.EditorErrorMessage);
        Assert.True(vm.HasEditorError);
        Assert.Equal("Dose", focusedField);

        // 3. Dose provided, Missing Frequency
        vm.Dose = "1 tab";
        vm.AddOrUpdateMedicineCommand.Execute(null);
        Assert.Equal("Frequency is required (e.g. Once daily, Twice daily, Three times daily).", vm.EditorErrorMessage);
        Assert.True(vm.HasEditorError);
        Assert.Equal("Frequency", focusedField);

        // 4. Frequency provided -> succeeds and clears error
        vm.Frequency = "Once daily";
        vm.AddOrUpdateMedicineCommand.Execute(null);
        Assert.Null(vm.EditorErrorMessage);
        Assert.False(vm.HasEditorError);
        Assert.Single(vm.PrescribedMedicines);
    }

    [Fact]
    public async Task FinalizePrescription_WithPendingUnaddedMedicine_ShowsGuardPrompt_CancelAborts()
    {
        var vm = CreateViewModel();
        vm.SelectedPatient = new PatientDto(
            Id: 1, RecordNumber: "MRN-001", Name: "Test Patient", DateOfBirth: null, Age: 30,
            Gender: Gender.Male, Phone: null, Address: null, MedicalHistoryNotes: null,
            KnownAllergies: null, CreatedAtUtc: DateTime.UtcNow, LastVisitDate: null,
            IsArchived: false, ArchivedAtUtc: null);

        // Pending editor content
        vm.MedicineName = "Brufen";
        vm.Dose = "1 tab";
        vm.Frequency = "Twice daily";

        // Doctor chooses Cancel (null)
        _dialogService.ConfirmationWithCancelResult = null;

        await vm.FinalizePrescriptionCommand.ExecuteAsync(null);

        Assert.NotNull(_dialogService.LastConfirmationWithCancelCall);
        Assert.Contains("You have a medicine in the editor that has not been added.", _dialogService.LastConfirmationWithCancelCall.Value.Message);
        Assert.Equal("Add medicine", _dialogService.LastConfirmationWithCancelCall.Value.YesText);
        Assert.Equal("Discard editor", _dialogService.LastConfirmationWithCancelCall.Value.NoText);
        Assert.Equal("Cancel", _dialogService.LastConfirmationWithCancelCall.Value.CancelText);

        // Editor was kept intact, nothing was finalized
        Assert.Equal("Brufen", vm.MedicineName);
        Assert.Empty(vm.PrescribedMedicines);
    }

    [Fact]
    public async Task FinalizePrescription_WithPendingUnaddedMedicine_ShowsGuardPrompt_DiscardClearsEditor()
    {
        var vm = CreateViewModel();
        vm.SelectedPatient = new PatientDto(
            Id: 1, RecordNumber: "MRN-001", Name: "Test Patient", DateOfBirth: null, Age: 30,
            Gender: Gender.Male, Phone: null, Address: null, MedicalHistoryNotes: null,
            KnownAllergies: null, CreatedAtUtc: DateTime.UtcNow, LastVisitDate: null,
            IsArchived: false, ArchivedAtUtc: null);

        // Add 1 valid medicine first
        vm.MedicineName = "Panadol";
        vm.Dose = "1 tab";
        vm.Frequency = "Once daily";
        vm.AddOrUpdateMedicineCommand.Execute(null);
        Assert.Single(vm.PrescribedMedicines);

        // Start editing another medicine in editor without clicking Add
        vm.MedicineName = "Brufen";
        vm.Dose = "1 tab";

        // Doctor chooses Discard (false)
        _dialogService.ConfirmationWithCancelResult = false;

        await vm.FinalizePrescriptionCommand.ExecuteAsync(null);

        Assert.NotNull(_dialogService.LastConfirmationWithCancelCall);
        // Editor is cleared
        Assert.True(string.IsNullOrEmpty(vm.MedicineName));
        // Prescription is finalized with the 1 existing medicine
        Assert.Null(vm.ValidationErrorMessage);
    }

    [Fact]
    public async Task FinalizePrescription_WithPendingUnaddedMedicine_ShowsGuardPrompt_AddAddsMedicineAndFinalizes()
    {
        var vm = CreateViewModel();
        vm.SelectedPatient = new PatientDto(
            Id: 1, RecordNumber: "MRN-001", Name: "Test Patient", DateOfBirth: null, Age: 30,
            Gender: Gender.Male, Phone: null, Address: null, MedicalHistoryNotes: null,
            KnownAllergies: null, CreatedAtUtc: DateTime.UtcNow, LastVisitDate: null,
            IsArchived: false, ArchivedAtUtc: null);

        // Doctor typed medicine completely in editor but forgot to click "Add to Prescription"
        vm.MedicineName = "Augmentin";
        vm.Dose = "1 tab";
        vm.Frequency = "Twice daily";

        // Doctor clicks Finalize and selects "Add medicine" (true)
        _dialogService.ConfirmationWithCancelResult = true;

        await vm.FinalizePrescriptionCommand.ExecuteAsync(null);

        Assert.NotNull(_dialogService.LastConfirmationWithCancelCall);
        // Medicine was added to prescribed medicines and finalized
        Assert.Single(vm.PrescribedMedicines);
        Assert.Equal("Augmentin", vm.PrescribedMedicines[0].MedicineName);
        Assert.Null(vm.ValidationErrorMessage);
    }

    private class TestDialogService : IDialogService
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Infos { get; } = new();
        public bool ConfirmationResult { get; set; } = true;
        public bool? ConfirmationWithCancelResult { get; set; } = true;
        public (string Title, string Message, string YesText, string NoText, string CancelText)? LastConfirmationWithCancelCall { get; set; }

        public void ShowError(string title, string message) => Errors.Add($"{title}: {message}");
        public void ShowWarning(string title, string message) => Warnings.Add($"{title}: {message}");
        public void ShowInformation(string title, string message) => Infos.Add($"{title}: {message}");
        public bool ShowConfirmation(string title, string message) => ConfirmationResult;
        public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel")
        {
            LastConfirmationWithCancelCall = (title, message, yesText, noText, cancelText);
            return ConfirmationWithCancelResult;
        }
    }

    private class TestNavigationService : INavigationService
    {
        public ViewModelBase? CurrentViewModel { get; set; }
        public NavigationDestination CurrentDestination { get; set; }
#pragma warning disable CS0067
        public event Action<ViewModelBase>? CurrentViewModelChanged;
#pragma warning restore CS0067

        public NavigationDestination? LastNavigatedDestination { get; private set; }
        public object? LastNavigatedParameter { get; private set; }

        public void NavigateTo(NavigationDestination destination, object? parameter = null)
        {
            LastNavigatedDestination = destination;
            LastNavigatedParameter = parameter;
            CurrentDestination = destination;
        }
    }

    private class TestQuickPhrasesService : IQuickPhrasesService
    {
        public List<string> Phrases { get; set; } = new();

        public Task<IReadOnlyList<string>> GetQuickPhrasesAsync()
        {
            return Task.FromResult<IReadOnlyList<string>>(Phrases.ToList());
        }

        public Task SaveQuickPhrasesAsync(IEnumerable<string> phrases)
        {
            Phrases = phrases.ToList();
            return Task.CompletedTask;
        }
    }
}
