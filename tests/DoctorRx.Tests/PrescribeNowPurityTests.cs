using System;
using System.IO;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class PrescribeNowPurityTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dbPath;
    private readonly TestDbContextFactory _factory;
    private readonly IPatientService _patientService;
    private readonly IMedicineService _medicineService;
    private readonly IPrescriptionService _prescriptionService;
    private readonly IDraftService _draftService;
    private readonly IPrescriptionComposerValidator _validator;
    private readonly IClock _clock;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    public PrescribeNowPurityTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"DoctorRx_PrescribeNow_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");

        var connStr = $"Data Source={_dbPath}";
        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(connStr)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;
        _factory = new TestDbContextFactory(options);

        using (var ctx = _factory.CreateDbContext())
        {
            ctx.Database.Migrate();
        }

        _clock = new SystemClock();
        var uowFactory = new UnitOfWorkFactory(_factory);
        _patientService = new PatientService(uowFactory, _clock, NullLogger<PatientService>.Instance);
        var searchService = new MedicineSearchService(uowFactory, NullLogger<MedicineSearchService>.Instance);
        _medicineService = new MedicineService(uowFactory, _clock, NullLogger<MedicineService>.Instance, searchService);
        _prescriptionService = new PrescriptionService(uowFactory, _clock, NullLogger<PrescriptionService>.Instance);
        _draftService = new DraftService(uowFactory, _clock, NullLogger<DraftService>.Instance);
        _validator = new PrescriptionComposerValidator(_clock);
        _dialogService = new StubDialogService();
        _navigationService = new StubNavigationService();
    }

    public void Dispose()
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    private NewPrescriptionViewModel CreateViewModel()
    {
        return new NewPrescriptionViewModel(
            _patientService,
            _medicineService,
            _prescriptionService,
            _draftService,
            _dialogService,
            _navigationService,
            _validator,
            _clock);
    }

    [Fact]
    public async Task PrescribeNow_LeavesAllSevenClinicalFieldsEmpty()
    {
        var vm = CreateViewModel();

        var catalogMed = new MedicineDto(
            Id: 99,
            Name: "Brufen",
            GenericName: "Ibuprofen",
            Form: "Suspension",
            Strength: "100 mg/5ml",
            IsActive: true,
            UsageCount: 5);

        // Act: Navigate to composer via Prescribe Now
        await vm.InitializeAsync(catalogMed);

        // Assert: Identity fields loaded
        Assert.Equal("Brufen", vm.MedicineName);
        Assert.Equal("Ibuprofen", vm.GenericName);
        Assert.Equal("Suspension", vm.Form);
        Assert.Equal("100 mg/5ml", vm.Strength);

        // Assert: All 7 clinical fields remain completely empty / unprescribed
        // 1. Dose
        Assert.True(string.IsNullOrEmpty(vm.Dose), "Dose must be empty on arrival via Prescribe Now.");
        // 2. Frequency
        Assert.True(string.IsNullOrEmpty(vm.Frequency), "Frequency must be empty on arrival via Prescribe Now.");
        // 3. Duration
        Assert.True(string.IsNullOrEmpty(vm.Duration), "Duration must be empty on arrival via Prescribe Now.");
        // 4. Route
        Assert.True(string.IsNullOrEmpty(vm.Route), "Route must be empty on arrival via Prescribe Now.");
        // 5. Timing
        Assert.True(string.IsNullOrEmpty(vm.Timing), "Timing must be empty on arrival via Prescribe Now.");
        // 6. Meal Relation
        Assert.Equal(MealRelation.AsDirected, vm.MealRelation);
        Assert.True(string.IsNullOrEmpty(vm.CustomMealRelationText), "Meal relation text must be empty.");
        // 7. Instructions
        Assert.True(string.IsNullOrEmpty(vm.Instructions), "Instructions must be empty on arrival via Prescribe Now.");
    }

    [Fact]
    public async Task PrescribeNow_WhenOpenDirtyDraftExists_DoesNotDiscardDraft_AndLoadsIntoEditor()
    {
        var vm = CreateViewModel();

        // Arrange: Prepare an existing dirty draft in the composer
        var patientRes = await _patientService.CreatePatientAsync(new CreatePatientDto
        {
            Name = "Fatima Bibi",
            Gender = Gender.Female,
            DateOfBirth = new DateOnly(1995, 3, 10),
            Phone = "03111234567"
        });
        Assert.True(patientRes.IsSuccess);
        vm.SelectedPatient = patientRes.Value;
        vm.ChiefComplaints = "Severe headache and vomiting";

        // Add 1 medicine row to the open prescription draft
        vm.MedicineName = "Gravinate";
        vm.Form = "Tablet";
        vm.Strength = "50 mg";
        vm.Dose = "1 tab";
        vm.Frequency = "Stat";
        vm.Route = "Oral";
        vm.Duration = "1 day";
        vm.AddOrUpdateMedicineCommand.Execute(null);

        Assert.Single(vm.PrescribedMedicines);
        Assert.True(vm.HasUnsavedChanges);

        // Act: Arrive in composer through "Prescribe Now" with a new catalog medicine
        var catalogMed = new MedicineDto(
            Id: 50,
            Name: "Panadol",
            GenericName: "Paracetamol",
            Form: "Tablet",
            Strength: "500 mg",
            IsActive: true,
            UsageCount: 12);

        await vm.InitializeAsync(catalogMed);

        // Assert: Open draft is NOT discarded!
        Assert.NotNull(vm.SelectedPatient);
        Assert.Equal("Fatima Bibi", vm.SelectedPatient.Name);
        Assert.Equal("Severe headache and vomiting", vm.ChiefComplaints);
        Assert.Single(vm.PrescribedMedicines);
        Assert.Equal("Gravinate", vm.PrescribedMedicines[0].MedicineName);

        // And the new catalog medicine is safely loaded into the active composer editor
        Assert.Equal("Panadol", vm.MedicineName);
        Assert.Equal("Paracetamol", vm.GenericName);
        Assert.Equal("Tablet", vm.Form);
        Assert.Equal("500 mg", vm.Strength);
        Assert.True(string.IsNullOrEmpty(vm.Dose));
        Assert.True(string.IsNullOrEmpty(vm.Frequency));
    }

    private class StubDialogService : IDialogService
    {
        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
        public bool ShowConfirmation(string title, string message) => true;
        public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel") => true;
    }

    private class StubNavigationService : INavigationService
    {
        public NavigationDestination CurrentDestination => NavigationDestination.NewPrescription;
        public NavigationDestination? PreviousDestination => null;
        public ViewModelBase? CurrentViewModel => null;
        public event Action<ViewModelBase>? CurrentViewModelChanged { add { } remove { } }
        public void NavigateTo(NavigationDestination destination, object? parameter = null) { }
        public void GoBack() { }
    }
}
