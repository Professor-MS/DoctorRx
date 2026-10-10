using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using Xunit;

namespace DoctorRx.Tests;

public class MedicinesViewModelTests
{
    private readonly FakeMedicineService _medicineService = new();
    private readonly FakeDialogService _dialogService = new();
    private readonly FakeNavigationService _navService = new();

    private MedicinesViewModel CreateViewModel()
    {
        return new MedicinesViewModel(_medicineService, _dialogService, _navService);
    }

    [Fact]
    public async Task InitializeAsync_LoadsAllMedicinesAndCalculatesCounts()
    {
        // Arrange
        _medicineService.Medicines.Add(new MedicineDto(1, "Panadol", "Paracetamol", "Tablet", "500 mg", true, 42));
        _medicineService.Medicines.Add(new MedicineDto(2, "Augmentin", "Co-amoxiclav", "Tablet", "625 mg", true, 18));
        _medicineService.Medicines.Add(new MedicineDto(3, "Rocephin", "Ceftriaxone", "Injection", "1 g", true, 5));

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
        Assert.Equal(3, vm.TotalMedicinesCount);
        Assert.Equal(3, vm.FilteredMedicinesCount);
        Assert.True(vm.HasMedicines);
        Assert.False(vm.NoMedicinesFound);
        // Panadol has 42 usage count, should be first
        Assert.Equal("Panadol", vm.Medicines[0].Name);
    }

    [Fact]
    public async Task SearchQuery_FiltersByBrandOrGeneric()
    {
        // Arrange
        _medicineService.Medicines.Add(new MedicineDto(1, "Panadol", "Paracetamol", "Tablet", "500 mg", true, 42));
        _medicineService.Medicines.Add(new MedicineDto(2, "Augmentin", "Co-amoxiclav", "Tablet", "625 mg", true, 18));
        _medicineService.Medicines.Add(new MedicineDto(3, "Ciprobay", "Ciprofloxacin", "Tablet", "500 mg", true, 10));

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act - Search by generic name
        vm.SearchQuery = "amox";
        vm.ApplyFilters();
        // Directly invoke apply filter or verify
        Assert.Contains(vm.Medicines, m => m.Name == "Augmentin");
        Assert.DoesNotContain(vm.Medicines, m => m.Name == "Panadol");
    }

    [Fact]
    public async Task DosageFormFilter_FiltersAccurately()
    {
        // Arrange
        _medicineService.Medicines.Add(new MedicineDto(1, "Panadol", "Paracetamol", "Tablet", "500 mg", true, 42));
        _medicineService.Medicines.Add(new MedicineDto(2, "Brufen Syrup", "Ibuprofen", "Syrup", "100 mg/5ml", true, 15));
        _medicineService.Medicines.Add(new MedicineDto(3, "Rocephin", "Ceftriaxone", "Injection", "1 g", true, 5));

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act
        vm.SelectedFormFilter = "Injection";

        // Assert
        Assert.Single(vm.Medicines);
        Assert.Equal("Rocephin", vm.Medicines[0].Name);
    }

    [Fact]
    public async Task AddMedicine_ValidationBlocksEmptyFields()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.OpenAddDrawerCommand.Execute(null);
        Assert.True(vm.IsDrawerOpen);
        Assert.False(vm.IsEditing);

        // Name is empty
        vm.FormName = "";
        vm.FormDosageForm = "Tablet";

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveMedicineCommand).ExecuteAsync(null);

        Assert.True(vm.HasFormError);
        Assert.Equal("Medicine brand or name is required.", vm.FormErrorMessage);
        Assert.True(vm.IsDrawerOpen);
    }

    [Fact]
    public async Task AddMedicine_SuccessfullyAddsNewMedicineAndClosesDrawer()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.OpenAddDrawerCommand.Execute(null);
        vm.FormName = "Klaricid";
        vm.FormGenericName = "Clarithromycin";
        vm.FormDosageForm = "Tablet";
        vm.FormStrength = "500 mg";

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveMedicineCommand).ExecuteAsync(null);

        Assert.False(vm.IsDrawerOpen);
        Assert.Contains(vm.Medicines, m => m.Name == "Klaricid");
        Assert.Equal(1, _dialogService.InformationCount);
    }

    [Fact]
    public async Task EditMedicine_UpdatesProperties()
    {
        var initial = new MedicineDto(1, "Panadol", "Paracetamol", "Tablet", "500 mg", true, 10);
        _medicineService.Medicines.Add(initial);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.OpenEditDrawerCommand.Execute(initial);
        Assert.True(vm.IsDrawerOpen);
        Assert.True(vm.IsEditing);
        Assert.Equal("Panadol", vm.FormName);

        vm.FormStrength = "650 mg";
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveMedicineCommand).ExecuteAsync(null);

        Assert.False(vm.IsDrawerOpen);
        var updated = vm.Medicines.First(m => m.Id == 1);
        Assert.Equal("650 mg", updated.Strength);
    }

    [Fact]
    public void PrescribeNow_NavigatesToNewPrescription()
    {
        var med = new MedicineDto(1, "Panadol", "Paracetamol", "Tablet", "500 mg", true, 10);
        var vm = CreateViewModel();

        vm.PrescribeNowCommand.Execute(med);

        Assert.Equal(NavigationDestination.NewPrescription, _navService.LastDestination);
        Assert.Same(med, _navService.LastParameter);
    }

    [Fact]
    public async Task AddMedicine_DuplicateWarning_WhenUserConfirms_SavesWithAllowDuplicate()
    {
        // Arrange
        _medicineService.Medicines.Add(new MedicineDto(1, "Panadol", "Paracetamol", "Tablet", "500 mg", true, 5));
        _dialogService.ConfirmationResult = true; // User clicks 'Yes' to proceed

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.OpenAddDrawerCommand.Execute(null);
        vm.FormName = "Panadol";
        vm.FormDosageForm = "Tablet";
        vm.FormStrength = "500 mg";

        // Act
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveMedicineCommand).ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _dialogService.ConfirmationCount);
        Assert.Equal("Potential Duplicate Medicine", _dialogService.LastConfirmationTitle);
        Assert.False(vm.IsDrawerOpen);
        Assert.Equal(2, _medicineService.Medicines.Count);
        Assert.Equal(1, _dialogService.InformationCount);
    }

    [Fact]
    public async Task AddMedicine_DuplicateWarning_WhenUserCancels_KeepsDrawerOpenWithWarning()
    {
        // Arrange
        _medicineService.Medicines.Add(new MedicineDto(1, "Panadol", "Paracetamol", "Tablet", "500 mg", true, 5));
        _dialogService.ConfirmationResult = false; // User clicks 'No' to cancel

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.OpenAddDrawerCommand.Execute(null);
        vm.FormName = "Panadol";
        vm.FormDosageForm = "Tablet";
        vm.FormStrength = "500 mg";

        // Act
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveMedicineCommand).ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _dialogService.ConfirmationCount);
        Assert.True(vm.IsDrawerOpen);
        Assert.Single(_medicineService.Medicines);
    }

    [Fact]
    public async Task DeleteMedicine_ReferencedMedicine_PromptsWithHistoricalPrescriptionMessage()
    {
        // Arrange
        var med = new MedicineDto(1, "Panadol", "Paracetamol", "Tablet", "500 mg", true, 10);
        _medicineService.Medicines.Add(med);
        _medicineService.ReferencedMedicineIds.Add(1);
        _dialogService.ConfirmationResult = true;

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.DeleteMedicineCommand).ExecuteAsync(med);

        // Assert
        Assert.Equal(1, _dialogService.ConfirmationCount);
        Assert.Contains("historical prescription", _dialogService.LastConfirmationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("safely deactivated", _dialogService.LastConfirmationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(_medicineService.Medicines[0].IsActive);
    }
}

public class FakeMedicineService : IMedicineService
{
    public List<MedicineDto> Medicines { get; } = new();
    public HashSet<int> ReferencedMedicineIds { get; } = new();

    public Task<IReadOnlyList<MedicineDto>> GetMedicinesPagedAsync(int pageNumber, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MedicineDto>>(Medicines.ToList());
    }

    public Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MedicineDto>>(
            Medicines.Where(m => m.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                                 (m.GenericName != null && m.GenericName.Contains(query, System.StringComparison.OrdinalIgnoreCase))).ToList());
    }

    public Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(MedicineSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var filtered = Medicines.AsEnumerable();
        if (!criteria.IncludeInactive)
            filtered = filtered.Where(m => m.IsActive);
        if (!string.IsNullOrWhiteSpace(criteria.DosageForm) && criteria.DosageForm != "All Forms")
            filtered = filtered.Where(m => m.Form.Equals(criteria.DosageForm, System.StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(criteria.Query))
            filtered = filtered.Where(m => m.Name.Contains(criteria.Query, System.StringComparison.OrdinalIgnoreCase) ||
                                           (m.GenericName != null && m.GenericName.Contains(criteria.Query, System.StringComparison.OrdinalIgnoreCase)));

        return Task.FromResult<IReadOnlyList<MedicineDto>>(filtered.Take(criteria.MaxResults).ToList());
    }

    public Task<MedicineDto?> GetMedicineByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Medicines.FirstOrDefault(m => m.Id == id));
    }

    public Task<Result<MedicineDto>> CreateMedicineAsync(CreateMedicineDto dto, bool allowDuplicate = false, CancellationToken cancellationToken = default)
    {
        if (!allowDuplicate)
        {
            var dup = Medicines.FirstOrDefault(m =>
                string.Equals(m.Name.Trim(), dto.Name.Trim(), System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.Form.Trim(), dto.Form.Trim(), System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.Strength.Trim(), (dto.Strength ?? string.Empty).Trim(), System.StringComparison.OrdinalIgnoreCase));

            if (dup != null)
            {
                return Task.FromResult(Result<MedicineDto>.Failure(
                    $"A medicine with this formulation already exists in the catalog ('{dup.DisplayText}').",
                    ResultErrorCode.DuplicateWarning));
            }
        }

        int newId = Medicines.Count + 1;
        var med = new MedicineDto(newId, dto.Name, dto.GenericName, dto.Form, dto.Strength, true, 0);
        Medicines.Add(med);
        return Task.FromResult(Result<MedicineDto>.Success(med));
    }

    public Task<Result<MedicineDto>> UpdateMedicineAsync(UpdateMedicineDto dto, bool allowDuplicate = false, CancellationToken cancellationToken = default)
    {
        var existingIndex = Medicines.FindIndex(m => m.Id == dto.Id);
        if (existingIndex < 0) return Task.FromResult(Result<MedicineDto>.Failure("Not found"));

        if (!allowDuplicate)
        {
            var dup = Medicines.FirstOrDefault(m => m.Id != dto.Id &&
                string.Equals(m.Name.Trim(), dto.Name.Trim(), System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.Form.Trim(), dto.Form.Trim(), System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.Strength.Trim(), (dto.Strength ?? string.Empty).Trim(), System.StringComparison.OrdinalIgnoreCase));

            if (dup != null)
            {
                return Task.FromResult(Result<MedicineDto>.Failure(
                    $"Another medicine with this formulation already exists in the catalog ('{dup.DisplayText}').",
                    ResultErrorCode.DuplicateWarning));
            }
        }

        var updated = new MedicineDto(dto.Id, dto.Name, dto.GenericName, dto.Form, dto.Strength, dto.IsActive, Medicines[existingIndex].UsageCount);
        Medicines[existingIndex] = updated;
        return Task.FromResult(Result<MedicineDto>.Success(updated));
    }

    public Task<Result> DeleteMedicineAsync(int id, CancellationToken cancellationToken = default)
    {
        var existingIndex = Medicines.FindIndex(m => m.Id == id);
        if (existingIndex >= 0)
        {
            var current = Medicines[existingIndex];
            Medicines[existingIndex] = current with { IsActive = false };
            return Task.FromResult(Result.Success());
        }
        return Task.FromResult(Result.Failure("Not found"));
    }

    public Task<Result> PurgeMedicineAsync(int id, CancellationToken cancellationToken = default)
    {
        var existingIndex = Medicines.FindIndex(m => m.Id == id);
        if (existingIndex < 0) return Task.FromResult(Result.Failure("Not found"));

        if (ReferencedMedicineIds.Contains(id))
        {
            return Task.FromResult(Result.Failure("Cannot delete medicine: Referenced in prescriptions."));
        }

        Medicines.RemoveAt(existingIndex);
        return Task.FromResult(Result.Success());
    }

    public Task<MedicineUsageSummaryDto> GetMedicineUsageSummaryAsync(int id, CancellationToken cancellationToken = default)
    {
        var med = Medicines.FirstOrDefault(m => m.Id == id);
        bool isReferenced = ReferencedMedicineIds.Contains(id);
        return Task.FromResult(new MedicineUsageSummaryDto(
            id,
            med?.DisplayText ?? string.Empty,
            isReferenced,
            isReferenced ? 1 : 0,
            !isReferenced
        ));
    }
}

public class FakeDialogService : IDialogService
{
    public int InformationCount { get; private set; }
    public int ConfirmationCount { get; private set; }
    public bool ConfirmationResult { get; set; } = true;
    public string? LastConfirmationTitle { get; private set; }
    public string? LastConfirmationMessage { get; private set; }

    public void ShowInformation(string title, string message) => InformationCount++;
    public void ShowWarning(string title, string message) { }
    public void ShowError(string title, string message) { }
    public bool ShowConfirmation(string title, string message)
    {
        LastConfirmationTitle = title;
        LastConfirmationMessage = message;
        ConfirmationCount++;
        return ConfirmationResult;
    }
    public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel")
    {
        LastConfirmationTitle = title;
        LastConfirmationMessage = message;
        ConfirmationCount++;
        return ConfirmationResult;
    }
}

public class FakeNavigationService : INavigationService
{
    public ViewModelBase? CurrentViewModel { get; set; }
    public NavigationDestination? LastDestination { get; private set; }
    public object? LastParameter { get; private set; }
    public NavigationDestination CurrentDestination => LastDestination ?? NavigationDestination.Dashboard;
#pragma warning disable CS0067
    public event System.Action<ViewModelBase>? CurrentViewModelChanged;
#pragma warning restore CS0067

    public void NavigateTo(NavigationDestination destination, object? parameter = null)
    {
        LastDestination = destination;
        LastParameter = parameter;
    }

    public void GoBack() { }
}
