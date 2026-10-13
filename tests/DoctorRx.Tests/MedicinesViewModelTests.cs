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

    private MedicinesViewModel CreateViewModel(IMedicineCsvImportService? csvImportService = null)
    {
        return new MedicinesViewModel(_medicineService, _dialogService, _navService, csvImportService: csvImportService);
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

    [Fact]
    public async Task Pagination_DefaultPageSize_Is10_AndCalculatesPageRanges()
    {
        // Add 25 medicines
        for (int i = 1; i <= 25; i++)
        {
            _medicineService.Medicines.Add(new MedicineDto(i, $"Med {i:D2}", $"Generic {i:D2}", "Tablet", "500 mg", true, i));
        }

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.Equal(10, vm.SelectedPageSize);
        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(3, vm.TotalPages);
        Assert.Equal(25, vm.TotalMedicinesCount);
        Assert.Equal(10, vm.Medicines.Count);
        Assert.Equal("Showing 1–10 of 25 medicines", vm.RangeSummaryText);
        Assert.Equal("Page 1 of 3", vm.PageIndicatorText);
        Assert.False(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);
    }

    [Fact]
    public async Task Pagination_ChangingPageSize_UpdatesTableImmediatelyAndResetsToPage1()
    {
        for (int i = 1; i <= 25; i++)
        {
            _medicineService.Medicines.Add(new MedicineDto(i, $"Med {i:D2}", $"Generic {i:D2}", "Tablet", "500 mg", true, i));
        }

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Navigate to page 2 first
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.NextPageCommand).ExecuteAsync(null);
        Assert.Equal(2, vm.CurrentPage);

        // Change page size to 20
        vm.SelectedPageSize = 20;

        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(2, vm.TotalPages);
        Assert.Equal(20, vm.Medicines.Count);
        Assert.Equal("Showing 1–20 of 25 medicines", vm.RangeSummaryText);
    }

    [Fact]
    public async Task Pagination_NextAndPreviousCommands_NavigatePagesCorrectly()
    {
        for (int i = 1; i <= 25; i++)
        {
            _medicineService.Medicines.Add(new MedicineDto(i, $"Med {i:D2}", $"Generic {i:D2}", "Tablet", "500 mg", true, i));
        }

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // On page 1: Previous disabled, Next enabled
        Assert.False(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);

        // Next to page 2
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.NextPageCommand).ExecuteAsync(null);
        Assert.Equal(2, vm.CurrentPage);
        Assert.True(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);
        Assert.Equal("Showing 11–20 of 25 medicines", vm.RangeSummaryText);

        // Next to page 3 (last page)
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.NextPageCommand).ExecuteAsync(null);
        Assert.Equal(3, vm.CurrentPage);
        Assert.True(vm.HasPreviousPage);
        Assert.False(vm.HasNextPage);
        Assert.Equal(5, vm.Medicines.Count);
        Assert.Equal("Showing 21–25 of 25 medicines", vm.RangeSummaryText);

        // Previous back to page 2
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.PreviousPageCommand).ExecuteAsync(null);
        Assert.Equal(2, vm.CurrentPage);
        Assert.Equal(10, vm.Medicines.Count);
    }

    [Fact]
    public async Task Pagination_DeletingItem_RecalculatesPageCountAndSafelyClampsCurrentPage()
    {
        // 21 medicines with page size 10 -> page 1 (10), page 2 (10), page 3 (1)
        for (int i = 1; i <= 21; i++)
        {
            _medicineService.Medicines.Add(new MedicineDto(i, $"Med {i:D2}", $"Generic {i:D2}", "Tablet", "500 mg", true, i));
        }

        _dialogService.ConfirmationResult = true;

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Navigate to page 3
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.NextPageCommand).ExecuteAsync(null);
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.NextPageCommand).ExecuteAsync(null);
        Assert.Equal(3, vm.CurrentPage);
        Assert.Single(vm.Medicines);

        // Delete the only item on page 3
        var onlyItem = vm.Medicines[0];
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.DeleteMedicineCommand).ExecuteAsync(onlyItem);

        // Page should safely clamp to page 2 (total 20 medicines -> 2 pages)
        Assert.Equal(2, vm.CurrentPage);
        Assert.Equal(2, vm.TotalPages);
        Assert.Equal(20, vm.TotalMedicinesCount);
        Assert.Equal(10, vm.Medicines.Count);
    }

    [Fact]
    public async Task SearchAndFilter_ResetsToPage1_AndPaginatesFilteredResults()
    {
        for (int i = 1; i <= 25; i++)
        {
            string name = i <= 15 ? $"Antibiotic {i:D2}" : $"Vitamin {i:D2}";
            _medicineService.Medicines.Add(new MedicineDto(i, name, "Generic", "Tablet", "500 mg", true, i));
        }

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Navigate to page 2
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.NextPageCommand).ExecuteAsync(null);
        Assert.Equal(2, vm.CurrentPage);

        // Apply search
        vm.SearchQuery = "Vitamin";
        vm.ApplyFilters();

        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(10, vm.TotalMedicinesCount); // 10 vitamins
        Assert.Equal(1, vm.TotalPages);
        Assert.Equal(10, vm.Medicines.Count);
        Assert.True(vm.Medicines.All(m => m.Name.Contains("Vitamin")));
    }

    [Fact]
    public async Task ImportCsvCommand_WhenServiceConfigured_ShowsResultSummary()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllText(tempFile, "header\nline");
            var fakeImportService = new FakeMedicineCsvImportService(new MedicineImportResult(90, 90, 0, 0, System.Array.Empty<string>()));
            var vm = CreateViewModel(fakeImportService);

            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<string?>)vm.ImportCsvCommand).ExecuteAsync(tempFile);

            Assert.Equal(1, _dialogService.InformationCount);
            Assert.Contains("Successfully Imported: 90", _dialogService.LastInformationMessage);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ToggleSortByNameCommand_TogglesAscendingAndDescending_AndSortsMedicines()
    {
        _medicineService.Medicines.Add(new MedicineDto(1, "Zithromax", "Azithromycin", "Tablet", "500 mg", true, 5));
        _medicineService.Medicines.Add(new MedicineDto(2, "Augmentin", "Amoxicillin/Clavulanate", "Tablet", "625 mg", true, 10));
        _medicineService.Medicines.Add(new MedicineDto(3, "Ciprobay", "Ciprofloxacin", "Tablet", "500 mg", true, 2));

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Default sort is by usage count desc: Augmentin (10), Zithromax (5), Ciprobay (2)
        Assert.Equal("Augmentin", vm.Medicines[0].Name);
        Assert.Equal("Zithromax", vm.Medicines[1].Name);
        Assert.Equal("Ciprobay", vm.Medicines[2].Name);

        // Toggle to name_asc
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.ToggleSortByNameCommand).ExecuteAsync(null);
        Assert.Equal("name_asc", vm.SortOrder);
        Assert.Equal(" ▲", vm.SortIndicator);
        Assert.Equal("Augmentin", vm.Medicines[0].Name);
        Assert.Equal("Ciprobay", vm.Medicines[1].Name);
        Assert.Equal("Zithromax", vm.Medicines[2].Name);

        // Toggle to name_desc
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.ToggleSortByNameCommand).ExecuteAsync(null);
        Assert.Equal("name_desc", vm.SortOrder);
        Assert.Equal(" ▼", vm.SortIndicator);
        Assert.Equal("Zithromax", vm.Medicines[0].Name);
        Assert.Equal("Ciprobay", vm.Medicines[1].Name);
        Assert.Equal("Augmentin", vm.Medicines[2].Name);
    }

    [Fact]
    public void DosageFormBadgeColors_ProvideDifferentiableColorsForDistinctForms()
    {
        var tab = new MedicineDto(1, "Panadol", "Paracetamol", "Tablet", "500 mg", true);
        var inj = new MedicineDto(2, "Rocephin", "Ceftriaxone", "Injection", "1 g", true);
        var syp = new MedicineDto(3, "Brufen", "Ibuprofen", "Syrup", "100 mg/5ml", true);
        var drop = new MedicineDto(4, "Tobradex", "Tobramycin", "Eye Drops", "5 ml", true);
        var cap = new MedicineDto(5, "Omeprazole", "Omeprazole", "Capsule", "20 mg", true);
        var cream = new MedicineDto(6, "Betnovate", "Betamethasone", "Topical Cream", "15 g", true);
        var inhaler = new MedicineDto(7, "Ventolin", "Salbutamol", "Inhaler", "100 mcg", true);

        // Verify distinct background colors
        Assert.NotEqual(tab.FormBadgeBackground, inj.FormBadgeBackground);
        Assert.NotEqual(tab.FormBadgeBackground, syp.FormBadgeBackground);
        Assert.NotEqual(inj.FormBadgeBackground, drop.FormBadgeBackground);
        Assert.NotEqual(cap.FormBadgeBackground, syp.FormBadgeBackground);
        Assert.NotEqual(cream.FormBadgeBackground, inhaler.FormBadgeBackground);

        // Verify specific color families
        Assert.Equal("#EFF6FF", tab.FormBadgeBackground); // Blue
        Assert.Equal("#FEF2F2", inj.FormBadgeBackground); // Crimson
        Assert.Equal("#ECFDF5", syp.FormBadgeBackground); // Emerald
        Assert.Equal("#ECFEFF", drop.FormBadgeBackground); // Cyan
        Assert.Equal("#F5F3FF", cap.FormBadgeBackground); // Violet
        Assert.Equal("#FFFBEB", cream.FormBadgeBackground); // Amber
        Assert.Equal("#FAF5FF", inhaler.FormBadgeBackground); // Fuchsia
    }

    [Fact]
    public void GenericFormulaAndDescriptionDisplay_FormatsAccurately_WithoutDuplication()
    {
        var medWithDifferentGeneric = new MedicineDto(1, "Panadol", "Paracetamol", "Tablet", "500 mg", true, 0, "Pain and fever relief");
        Assert.Equal("Generic: Paracetamol", medWithDifferentGeneric.GenericFormulaDisplay);
        Assert.Equal("Pain and fever relief", medWithDifferentGeneric.DescriptionDisplay);

        // When name and generic are identical (e.g. Acyclovir), it must NOT repeat "Formula: Acyclovir" or "Tablet formulation"
        var medWithSameGeneric = new MedicineDto(2, "Acyclovir", "Acyclovir", "Tablet", "400 mg", true);
        Assert.Equal("Antiviral", medWithSameGeneric.GenericFormulaDisplay);
        Assert.Contains("Herpes simplex", medWithSameGeneric.DescriptionDisplay);
    }

    [Fact]
    public async Task Drawer_LoadsAndSavesDescription_ForBothAddAndEdit()
    {
        var existing = new MedicineDto(10, "Flagyl", "Metronidazole", "Tablet", "400 mg", true, 0, "Antiprotozoal & Antibacterial");
        _medicineService.Medicines.Add(existing);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Open edit drawer
        vm.OpenEditDrawerCommand.Execute(existing);
        Assert.True(vm.IsEditing);
        Assert.Equal("Antiprotozoal & Antibacterial", vm.FormDescription);

        // Edit description
        vm.FormDescription = "Updated indications";
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveMedicineCommand).ExecuteAsync(null);

        var updated = _medicineService.Medicines.First(m => m.Id == 10);
        Assert.Equal("Updated indications", updated.Description);

        // Open add drawer
        vm.OpenAddDrawerCommand.Execute(null);
        Assert.False(vm.IsEditing);
        Assert.Equal(string.Empty, vm.FormDescription);

        vm.FormName = "NewMed";
        vm.FormDosageForm = "Tablet";
        vm.FormStrength = "10 mg";
        vm.FormDescription = "New therapeutic description";
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveMedicineCommand).ExecuteAsync(null);

        var created = _medicineService.Medicines.First(m => m.Name == "NewMed");
        Assert.Equal("New therapeutic description", created.Description);
    }

    [Fact]
    public void MedicineDto_HasLongDescription_ReturnsTrueOnlyWhenDescriptionExceedsThreshold()
    {
        var shortMed = new MedicineDto(1, "Aspirin", "Aspirin", "Tablet", "75 mg", true, Description: "Pain relief");
        var longMed = new MedicineDto(2, "Acyclovir", "Acyclovir", "Tablet", "400 mg", true,
            Description: "Herpes simplex, genital herpes, shingles (herpes zoster) & chickenpox (varicella)");

        Assert.False(shortMed.HasLongDescription);
        Assert.True(longMed.HasLongDescription);
    }
}

public class FakeMedicineCsvImportService : IMedicineCsvImportService
{
    private readonly MedicineImportResult _result;
    public FakeMedicineCsvImportService(MedicineImportResult result) => _result = result;
    public Task<MedicineImportResult> ImportFromCsvAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(_result);
    public Task<MedicineImportResult> ImportFromStreamAsync(System.IO.Stream stream, CancellationToken cancellationToken = default) =>
        Task.FromResult(_result);
}

public class FakeMedicineService : IMedicineService
{
    public List<MedicineDto> Medicines { get; } = new();
    public HashSet<int> ReferencedMedicineIds { get; } = new();

    public Task<IReadOnlyList<MedicineDto>> GetMedicinesPagedAsync(int pageNumber, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MedicineDto>>(Medicines.ToList());
    }

    public Task<PagedResult<MedicineDto>> GetMedicinesPagedAsync(string? searchText, string? dosageForm, bool includeInactive, int pageNumber, int pageSize, string? sortOrder = null, CancellationToken cancellationToken = default)
    {
        var query = Medicines.AsEnumerable();
        if (!includeInactive)
            query = query.Where(m => m.IsActive);
        if (!string.IsNullOrWhiteSpace(dosageForm) && dosageForm != "All Forms")
        {
            if (dosageForm.Equals("Drops", StringComparison.OrdinalIgnoreCase))
                query = query.Where(m => m.Form.Contains("Drop", StringComparison.OrdinalIgnoreCase));
            else if (dosageForm.Equals("Cream / Ointment", StringComparison.OrdinalIgnoreCase))
                query = query.Where(m => m.Form.Contains("Cream", StringComparison.OrdinalIgnoreCase) || m.Form.Contains("Ointment", StringComparison.OrdinalIgnoreCase));
            else
                query = query.Where(m => m.Form.Equals(dosageForm, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var q = searchText.Trim();
            query = query.Where(m => m.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                     (!string.IsNullOrWhiteSpace(m.GenericName) && m.GenericName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                                     (!string.IsNullOrWhiteSpace(m.Strength) && m.Strength.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }
        var sorted = sortOrder switch
        {
            "name_asc" => query.OrderBy(m => m.Name),
            "name_desc" => query.OrderByDescending(m => m.Name),
            _ => query.OrderByDescending(m => m.UsageCount).ThenBy(m => m.Name)
        };
        var totalCount = sorted.Count();
        var paged = sorted.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<MedicineDto>(paged, totalCount, pageNumber, pageSize));
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
        var med = new MedicineDto(newId, dto.Name, dto.GenericName, dto.Form, dto.Strength, true, 0, dto.Description);
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

        var updated = new MedicineDto(dto.Id, dto.Name, dto.GenericName, dto.Form, dto.Strength, dto.IsActive, Medicines[existingIndex].UsageCount, dto.Description);
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
    public string? LastInformationTitle { get; private set; }
    public string? LastInformationMessage { get; private set; }

    public void ShowInformation(string title, string message)
    {
        LastInformationTitle = title;
        LastInformationMessage = message;
        InformationCount++;
    }
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
