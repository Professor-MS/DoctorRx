using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Presentation.ViewModels;

public class MedicinesViewModel : ViewModelBase
{
    public override NavigationSection NavigationSection => NavigationSection.Medicines;

    private readonly IMedicineService _medicineService;
    private readonly IMedicineSearchService? _medicineSearchService;
    private readonly IMedicineCsvImportService? _csvImportService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<MedicinesViewModel>? _logger;

    private CancellationTokenSource? _searchCts;

    // Search and Filtering
    private string _searchQuery = string.Empty;
    private string _selectedFormFilter = "All Forms";
    private bool _showInactive;
    private int _totalMedicinesCount;
    private int _filteredMedicinesCount;
    private string? _sortOrder;

    // Pagination
    private int _selectedPageSize = 10;
    private int _currentPage = 1;

    // Selection
    private MedicineDto? _selectedMedicine;

    // Drawer State
    private bool _isDrawerOpen;
    private bool _isEditing;
    private int _editingMedicineId;
    private string _formName = string.Empty;
    private string? _formGenericName;
    private string _formDosageForm = "Tablet";
    private string _formStrength = string.Empty;
    private string? _formDescription;
    private bool _formIsActive = true;
    private string? _formErrorMessage;

    // Form Quick Chips
    public ObservableCollection<string> AvailableDosageForms { get; } = new()
    {
        "Tablet", "Capsule", "Syrup", "Suspension", "Injection",
        "IV Infusion", "Eye Drops", "Ear Drops", "Topical Cream", "Ointment", "Inhaler"
    };

    public ObservableCollection<string> FilterDosageForms { get; } = new()
    {
        "All Forms", "Tablet", "Capsule", "Syrup", "Suspension",
        "Injection", "IV Infusion", "Drops", "Cream / Ointment", "Inhaler"
    };

    public ObservableCollection<string> CommonStrengthChips { get; } = new()
    {
        "250 mg", "500 mg", "625 mg", "1 g", "120 mg/5ml", "250 mg/5ml", "0.5%", "1%", "5 ml", "100 ml"
    };

    public ObservableCollection<int> AvailablePageSizes { get; } = new() { 10, 20, 30 };

    public ObservableCollection<MedicineDto> Medicines { get; } = new();

    #region Properties

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                DebounceSearch();
            }
        }
    }

    public string SelectedFormFilter
    {
        get => _selectedFormFilter;
        set
        {
            if (SetProperty(ref _selectedFormFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool ShowInactive
    {
        get => _showInactive;
        set
        {
            if (SetProperty(ref _showInactive, value))
            {
                ApplyFilters();
            }
        }
    }

    public string? SortOrder
    {
        get => _sortOrder;
        set
        {
            if (SetProperty(ref _sortOrder, value))
            {
                OnPropertyChanged(nameof(SortIndicator));
            }
        }
    }

    public string SortIndicator => _sortOrder switch
    {
        "name_asc" => " ▲",
        "name_desc" => " ▼",
        _ => " ⇅"
    };

    public int TotalMedicinesCount
    {
        get => _totalMedicinesCount;
        private set
        {
            if (SetProperty(ref _totalMedicinesCount, value))
            {
                NotifyPaginationState();
            }
        }
    }

    public int FilteredMedicinesCount
    {
        get => _filteredMedicinesCount;
        private set
        {
            if (SetProperty(ref _filteredMedicinesCount, value))
            {
                NotifyPaginationState();
            }
        }
    }

    public int SelectedPageSize
    {
        get => _selectedPageSize;
        set
        {
            if (SetProperty(ref _selectedPageSize, value))
            {
                CurrentPage = 1;
                _ = LoadMedicinesPagedAsync();
            }
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value))
            {
                NotifyPaginationState();
            }
        }
    }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalMedicinesCount / Math.Max(1, SelectedPageSize)));

    public bool HasPreviousPage => CurrentPage > 1 && !IsBusy;

    public bool HasNextPage => CurrentPage < TotalPages && !IsBusy;

    public string PageIndicatorText => $"Page {CurrentPage} of {TotalPages}";

    public string RangeSummaryText
    {
        get
        {
            if (TotalMedicinesCount == 0) return "Showing 0 medicines";
            var start = (CurrentPage - 1) * SelectedPageSize + 1;
            var end = Math.Min(CurrentPage * SelectedPageSize, TotalMedicinesCount);
            return $"Showing {start}–{end} of {TotalMedicinesCount:N0} medicines";
        }
    }

    public string FilterSummaryText => RangeSummaryText;

    public bool HasMedicines => FilteredMedicinesCount > 0;
    public bool NoMedicinesFound => FilteredMedicinesCount == 0 && !IsBusy;

    public MedicineDto? SelectedMedicine
    {
        get => _selectedMedicine;
        set => SetProperty(ref _selectedMedicine, value);
    }

    // Drawer Properties
    public bool IsDrawerOpen
    {
        get => _isDrawerOpen;
        set => SetProperty(ref _isDrawerOpen, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(DrawerTitle));
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }
    }

    public string DrawerTitle => IsEditing ? "Edit Medicine Formulary" : "Add New Medicine to Catalog";
    public string SaveButtonText => IsEditing ? "Update Formulation" : "Save to Catalog";

    public string FormName
    {
        get => _formName;
        set
        {
            if (SetProperty(ref _formName, value))
            {
                ValidateForm();
            }
        }
    }

    public string? FormGenericName
    {
        get => _formGenericName;
        set => SetProperty(ref _formGenericName, value);
    }

    public string FormDosageForm
    {
        get => _formDosageForm;
        set
        {
            if (SetProperty(ref _formDosageForm, value))
            {
                ValidateForm();
            }
        }
    }

    public string FormStrength
    {
        get => _formStrength;
        set => SetProperty(ref _formStrength, value);
    }

    public string? FormDescription
    {
        get => _formDescription;
        set => SetProperty(ref _formDescription, value);
    }

    public bool FormIsActive
    {
        get => _formIsActive;
        set => SetProperty(ref _formIsActive, value);
    }

    public string? FormErrorMessage
    {
        get => _formErrorMessage;
        set
        {
            if (SetProperty(ref _formErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasFormError));
            }
        }
    }

    public bool HasFormError => !string.IsNullOrWhiteSpace(FormErrorMessage);

    public double DrawerWidth => IsCompact ? 320 : 420;

    public bool CanLoadMore => HasNextPage;

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand ImportCsvCommand { get; }
    public ICommand OpenAddDrawerCommand { get; }
    public ICommand OpenEditDrawerCommand { get; }
    public ICommand CloseDrawerCommand { get; }
    public ICommand SaveMedicineCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public ICommand DeleteMedicineCommand { get; }
    public ICommand PrescribeNowCommand { get; }
    public ICommand SelectFormChipCommand { get; }
    public ICommand SelectStrengthChipCommand { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand ToggleSortByNameCommand { get; }

    #endregion

    public MedicinesViewModel()
    {
        // Parameterless constructor for XAML designer & test runner
        _medicineService = null!;
        _dialogService = null!;
        _navigationService = null!;

        RefreshCommand = new RelayCommand(() => { });
        PreviousPageCommand = new RelayCommand(() => { });
        NextPageCommand = new RelayCommand(() => { });
        ImportCsvCommand = new RelayCommand(() => { });
        OpenAddDrawerCommand = new RelayCommand(() => { });
        OpenEditDrawerCommand = new RelayCommand<MedicineDto>(_ => { });
        CloseDrawerCommand = new RelayCommand(() => { });
        SaveMedicineCommand = new RelayCommand(() => { });
        ToggleActiveCommand = new RelayCommand<MedicineDto>(_ => { });
        DeleteMedicineCommand = new RelayCommand<MedicineDto>(_ => { });
        PrescribeNowCommand = new RelayCommand<MedicineDto>(_ => { });
        SelectFormChipCommand = new RelayCommand<string>(_ => { });
        SelectStrengthChipCommand = new RelayCommand<string>(_ => { });
        LoadMoreCommand = new RelayCommand(() => { });
        ToggleSortByNameCommand = new RelayCommand(() => { });
    }

    public MedicinesViewModel(
        IMedicineService medicineService,
        IDialogService dialogService,
        INavigationService navigationService,
        ILogger<MedicinesViewModel>? logger = null,
        IMedicineSearchService? medicineSearchService = null,
        IMedicineCsvImportService? csvImportService = null)
    {
        _medicineService = medicineService;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _logger = logger;
        _medicineSearchService = medicineSearchService;
        _csvImportService = csvImportService;

        RefreshCommand = new AsyncRelayCommand(() => LoadMedicinesPagedAsync());
        PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync, () => HasPreviousPage);
        NextPageCommand = new AsyncRelayCommand(NextPageAsync, () => HasNextPage);
        ImportCsvCommand = new AsyncRelayCommand<string?>(ImportCsvAsync);
        OpenAddDrawerCommand = new RelayCommand(OpenAddDrawer);
        OpenEditDrawerCommand = new RelayCommand<MedicineDto>(OpenEditDrawer);
        CloseDrawerCommand = new RelayCommand(CloseDrawer);
        SaveMedicineCommand = new AsyncRelayCommand(SaveMedicineAsync);
        ToggleActiveCommand = new AsyncRelayCommand<MedicineDto>(ToggleActiveAsync);
        DeleteMedicineCommand = new AsyncRelayCommand<MedicineDto>(DeleteMedicineAsync);
        PrescribeNowCommand = new RelayCommand<MedicineDto>(PrescribeNow);

        SelectFormChipCommand = new RelayCommand<string>(chip =>
        {
            if (!string.IsNullOrWhiteSpace(chip))
            {
                FormDosageForm = chip;
            }
        });

        SelectStrengthChipCommand = new RelayCommand<string>(chip =>
        {
            if (!string.IsNullOrWhiteSpace(chip))
            {
                FormStrength = chip;
            }
        });

        LoadMoreCommand = NextPageCommand;
        ToggleSortByNameCommand = new AsyncRelayCommand(ToggleSortByNameAsync);
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        await LoadMedicinesPagedAsync();
    }

    protected override void OnLayoutModeChanged(LayoutMode newMode)
    {
        OnPropertyChanged(nameof(DrawerWidth));
    }

    public Task LoadMedicinesAsync() => LoadMedicinesPagedAsync();

    public async Task ToggleSortByNameAsync()
    {
        if (SortOrder == "name_asc")
        {
            SortOrder = "name_desc";
        }
        else
        {
            SortOrder = "name_asc";
        }

        CurrentPage = 1;
        await LoadMedicinesPagedAsync();
    }

    public async Task LoadMedicinesPagedAsync()
    {
        if (_medicineService == null) return;

        try
        {
            IsBusy = true;
            BusyMessage = "Loading medicines catalog...";

            var result = await _medicineService.GetMedicinesPagedAsync(
                _searchQuery,
                _selectedFormFilter,
                _showInactive,
                CurrentPage,
                SelectedPageSize,
                SortOrder);

            int total = result.TotalCount;
            TotalMedicinesCount = total;
            FilteredMedicinesCount = total;

            int calculatedTotalPages = Math.Max(1, (int)Math.Ceiling((double)total / Math.Max(1, SelectedPageSize)));
            if (CurrentPage > calculatedTotalPages && total > 0)
            {
                CurrentPage = calculatedTotalPages;
                result = await _medicineService.GetMedicinesPagedAsync(
                    _searchQuery,
                    _selectedFormFilter,
                    _showInactive,
                    CurrentPage,
                    SelectedPageSize,
                    SortOrder);
                TotalMedicinesCount = result.TotalCount;
                FilteredMedicinesCount = result.TotalCount;
            }

            Medicines.Clear();
            foreach (var item in result.Items)
            {
                Medicines.Add(item);
            }

            NotifyPaginationState();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load medicines");
            _dialogService?.ShowError("Catalog Error", "Unable to load medicines catalog from database.");
        }
        finally
        {
            IsBusy = false;
            NotifyPaginationState();
        }
    }

    private void NotifyPaginationState()
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(PageIndicatorText));
        OnPropertyChanged(nameof(RangeSummaryText));
        OnPropertyChanged(nameof(FilterSummaryText));
        OnPropertyChanged(nameof(HasMedicines));
        OnPropertyChanged(nameof(NoMedicinesFound));
        OnPropertyChanged(nameof(CanLoadMore));
        (PreviousPageCommand as IRelayCommand)?.NotifyCanExecuteChanged();
        (NextPageCommand as IRelayCommand)?.NotifyCanExecuteChanged();
    }

    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadMedicinesPagedAsync();
        }
    }

    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadMedicinesPagedAsync();
        }
    }

    private void DebounceSearch()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (!token.IsCancellationRequested)
                {
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(ApplyFilters);
                    }
                    else
                    {
                        ApplyFilters();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Debounce cancelled
            }
        }, token);
    }

    public void ApplyFilters()
    {
        _searchCts?.Cancel();
        CurrentPage = 1;
        _ = LoadMedicinesPagedAsync();
    }

    public async Task ImportCsvAsync(string? explicitPath = null)
    {
        if (_csvImportService == null)
        {
            _dialogService?.ShowError("Import Error", "CSV Import service is not configured.");
            return;
        }

        string? filePath = explicitPath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            filePath = FindPakistanMedicineMasterCsv();

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                try
                {
                    var openDialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Select Pakistan Medicine Master CSV",
                        Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                        FileName = "pakistan_medicine_master.csv",
                        InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
                    };

                    if (openDialog.ShowDialog() == true)
                    {
                        filePath = openDialog.FileName;
                    }
                }
                catch
                {
                    // Headless or UI thread limitation fallback
                }
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _dialogService?.ShowWarning(
                "CSV File Missing",
                "Could not find 'pakistan_medicine_master.csv'.\n\nPlease place the file in the application directory, or select it using the file dialog.");
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Importing medicines from CSV...";

            var result = await _csvImportService.ImportFromCsvAsync(filePath);

            var summary = $"Import completed.\n\n" +
                          $"• Successfully Imported: {result.ImportedCount:N0}\n" +
                          $"• Skipped (Duplicates): {result.SkippedCount:N0}\n" +
                          $"• Rejected (Invalid): {result.RejectedCount:N0}\n" +
                          $"• Total Processed: {result.TotalProcessed:N0}";

            if (result.ErrorMessages.Count > 0)
            {
                summary += "\n\nSample errors / warnings:\n" +
                           string.Join("\n", result.ErrorMessages.Take(5));
                if (result.ErrorMessages.Count > 5)
                {
                    summary += $"\n...and {result.ErrorMessages.Count - 5} more issues.";
                }
            }

            _dialogService?.ShowInformation("Medicine Import Summary", summary);

            CurrentPage = 1;
            await LoadMedicinesPagedAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to import medicines CSV from {FilePath}", filePath);
            _dialogService?.ShowError("Import Failed", $"An unexpected error occurred during import: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            NotifyPaginationState();
        }
    }

    private static string? FindPakistanMedicineMasterCsv()
    {
        const string fileName = "pakistan_medicine_master.csv";

        var current = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(current, fileName)))
            return Path.Combine(current, fileName);

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        if (File.Exists(Path.Combine(baseDir, fileName)))
            return Path.Combine(baseDir, fileName);

        var dir = new DirectoryInfo(baseDir);
        for (int i = 0; i < 5 && dir != null; i++)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    private void OpenAddDrawer()
    {
        IsEditing = false;
        _editingMedicineId = 0;
        FormName = string.Empty;
        FormGenericName = string.Empty;
        FormDosageForm = "Tablet";
        FormStrength = string.Empty;
        FormDescription = string.Empty;
        FormIsActive = true;
        FormErrorMessage = null;
        IsDrawerOpen = true;
    }

    private void OpenEditDrawer(MedicineDto? dto)
    {
        if (dto == null) return;

        IsEditing = true;
        _editingMedicineId = dto.Id;
        FormName = dto.Name;
        FormGenericName = dto.GenericName ?? string.Empty;
        FormDosageForm = dto.Form;
        FormStrength = dto.Strength;
        FormDescription = dto.Description ?? string.Empty;
        FormIsActive = dto.IsActive;
        FormErrorMessage = null;
        IsDrawerOpen = true;
    }

    private void CloseDrawer()
    {
        IsDrawerOpen = false;
        FormErrorMessage = null;
    }

    private void ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            FormErrorMessage = "Medicine brand or name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormDosageForm))
        {
            FormErrorMessage = "Dosage form is required.";
            return;
        }

        FormErrorMessage = null;
    }

    private async Task SaveMedicineAsync()
    {
        ValidateForm();
        if (HasFormError) return;

        try
        {
            IsBusy = true;

            if (IsEditing)
            {
                var updateDto = new UpdateMedicineDto
                {
                    Id = _editingMedicineId,
                    Name = FormName.Trim(),
                    GenericName = string.IsNullOrWhiteSpace(FormGenericName) ? null : FormGenericName.Trim(),
                    Form = FormDosageForm.Trim(),
                    Strength = FormStrength?.Trim() ?? string.Empty,
                    Description = string.IsNullOrWhiteSpace(FormDescription) ? null : FormDescription.Trim(),
                    IsActive = FormIsActive
                };

                var result = await _medicineService.UpdateMedicineAsync(updateDto);
                if (result.IsSuccess)
                {
                    _dialogService?.ShowInformation("Medicine Updated", $"'{FormName}' has been updated in the catalog.");
                    CloseDrawer();
                    await LoadMedicinesPagedAsync();
                }
                else if (result.ErrorCode == DoctorRx.Application.Common.ResultErrorCode.DuplicateWarning)
                {
                    var warningMessage = result.ErrorMessage ?? "A medicine with this formulation already exists.";
                    var proceed = _dialogService?.ShowConfirmation(
                        "Potential Duplicate Medicine",
                        $"{warningMessage}\n\nDo you want to proceed and save this formulation anyway?") ?? false;

                    if (proceed)
                    {
                        var dupResult = await _medicineService.UpdateMedicineAsync(updateDto, allowDuplicate: true);
                        if (dupResult.IsSuccess)
                        {
                            _dialogService?.ShowInformation("Medicine Updated", $"'{FormName}' has been updated in the catalog.");
                            CloseDrawer();
                            await LoadMedicinesPagedAsync();
                        }
                        else
                        {
                            FormErrorMessage = dupResult.ErrorMessage ?? "Failed to update medicine.";
                        }
                    }
                }
                else
                {
                    FormErrorMessage = result.ErrorMessage ?? "Failed to update medicine.";
                }
            }
            else
            {
                var createDto = new CreateMedicineDto
                {
                    Name = FormName.Trim(),
                    GenericName = string.IsNullOrWhiteSpace(FormGenericName) ? null : FormGenericName.Trim(),
                    Form = FormDosageForm.Trim(),
                    Strength = FormStrength?.Trim() ?? string.Empty,
                    Description = string.IsNullOrWhiteSpace(FormDescription) ? null : FormDescription.Trim()
                };

                var result = await _medicineService.CreateMedicineAsync(createDto);
                if (result.IsSuccess)
                {
                    _dialogService?.ShowInformation("Medicine Added", $"'{FormName}' is now available for prescribing.");
                    CloseDrawer();
                    await LoadMedicinesPagedAsync();
                }
                else if (result.ErrorCode == DoctorRx.Application.Common.ResultErrorCode.DuplicateWarning)
                {
                    var warningMessage = result.ErrorMessage ?? "A medicine with this formulation already exists.";
                    var proceed = _dialogService?.ShowConfirmation(
                        "Potential Duplicate Medicine",
                        $"{warningMessage}\n\nDo you want to proceed and add this formulation anyway?") ?? false;

                    if (proceed)
                    {
                        var dupResult = await _medicineService.CreateMedicineAsync(createDto, allowDuplicate: true);
                        if (dupResult.IsSuccess)
                        {
                            _dialogService?.ShowInformation("Medicine Added", $"'{FormName}' is now available for prescribing.");
                            CloseDrawer();
                            await LoadMedicinesPagedAsync();
                        }
                        else
                        {
                            FormErrorMessage = dupResult.ErrorMessage ?? "Failed to save medicine.";
                        }
                    }
                }
                else
                {
                    FormErrorMessage = result.ErrorMessage ?? "Failed to save medicine.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while saving medicine");
            FormErrorMessage = "An unexpected error occurred while saving.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ToggleActiveAsync(MedicineDto? dto)
    {
        if (dto == null || _medicineService == null) return;

        try
        {
            var updateDto = new UpdateMedicineDto
            {
                Id = dto.Id,
                Name = dto.Name,
                GenericName = dto.GenericName,
                Form = dto.Form,
                Strength = dto.Strength,
                IsActive = !dto.IsActive
            };

            var result = await _medicineService.UpdateMedicineAsync(updateDto, allowDuplicate: true);
            if (result.IsSuccess)
            {
                await LoadMedicinesPagedAsync();
            }
            else
            {
                _dialogService?.ShowError("Update Failed", result.ErrorMessage ?? "Could not change status.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to toggle active status for #{Id}", dto.Id);
        }
    }

    private async Task DeleteMedicineAsync(MedicineDto? dto)
    {
        if (dto == null || _medicineService == null) return;

        var summary = await _medicineService.GetMedicineUsageSummaryAsync(dto.Id);
        bool isReferenced = summary.IsReferencedInPrescriptions || summary.IsReferencedInDrafts;
        string message = isReferenced
            ? $"'{dto.DisplayText}' is referenced in {summary.PrescriptionReferenceCount} historical prescription(s) or saved drafts.\n\nIt will be safely deactivated from the catalog and hidden from future prescribing. Historical prescriptions will remain completely intact.\n\nProceed with deactivation?"
            : $"Are you sure you want to permanently remove '{dto.DisplayText}' from the catalog?\n\nThis medicine has never been prescribed or drafted.\n\nProceed with permanent deletion?";

        var confirmed = _dialogService?.ShowConfirmation(isReferenced ? "Deactivate Medicine" : "Delete Medicine", message) ?? false;
        if (!confirmed) return;

        try
        {
            var result = isReferenced
                ? await _medicineService.DeleteMedicineAsync(dto.Id)
                : await _medicineService.PurgeMedicineAsync(dto.Id);

            if (result.IsSuccess)
            {
                await LoadMedicinesPagedAsync();
            }
            else
            {
                _dialogService?.ShowError(isReferenced ? "Deactivation Failed" : "Delete Failed", result.ErrorMessage ?? "Could not remove medicine.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to remove medicine #{Id}", dto.Id);
        }
    }

    private void PrescribeNow(MedicineDto? dto)
    {
        if (dto == null || _navigationService == null) return;

        // Navigate to New Prescription pre-loaded with the selected catalog medicine
        _navigationService.NavigateTo(NavigationDestination.NewPrescription, dto);
    }
}
