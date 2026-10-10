using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<MedicinesViewModel>? _logger;

    private CancellationTokenSource? _searchCts;
    private List<MedicineDto> _allLoadedMedicines = new();

    // Search and Filtering
    private string _searchQuery = string.Empty;
    private string _selectedFormFilter = "All Forms";
    private bool _showInactive;
    private int _totalMedicinesCount;
    private int _filteredMedicinesCount;

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

    public int TotalMedicinesCount
    {
        get => _totalMedicinesCount;
        private set => SetProperty(ref _totalMedicinesCount, value);
    }

    public int FilteredMedicinesCount
    {
        get => _filteredMedicinesCount;
        private set
        {
            if (SetProperty(ref _filteredMedicinesCount, value))
            {
                OnPropertyChanged(nameof(FilterSummaryText));
                OnPropertyChanged(nameof(HasMedicines));
                OnPropertyChanged(nameof(NoMedicinesFound));
            }
        }
    }

    public string FilterSummaryText
    {
        get
        {
            if (TotalMedicinesCount == 0) return "Catalog is empty";
            if (FilteredMedicinesCount == TotalMedicinesCount)
                return $"Showing all {TotalMedicinesCount} medicines";
            return $"Showing {FilteredMedicinesCount} of {TotalMedicinesCount} medicines";
        }
    }

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

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand OpenAddDrawerCommand { get; }
    public ICommand OpenEditDrawerCommand { get; }
    public ICommand CloseDrawerCommand { get; }
    public ICommand SaveMedicineCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public ICommand DeleteMedicineCommand { get; }
    public ICommand PrescribeNowCommand { get; }
    public ICommand SelectFormChipCommand { get; }
    public ICommand SelectStrengthChipCommand { get; }

    #endregion

    public MedicinesViewModel()
    {
        // Parameterless constructor for XAML designer & test runner
        _medicineService = null!;
        _dialogService = null!;
        _navigationService = null!;

        RefreshCommand = new RelayCommand(() => { });
        OpenAddDrawerCommand = new RelayCommand(() => { });
        OpenEditDrawerCommand = new RelayCommand<MedicineDto>(_ => { });
        CloseDrawerCommand = new RelayCommand(() => { });
        SaveMedicineCommand = new RelayCommand(() => { });
        ToggleActiveCommand = new RelayCommand<MedicineDto>(_ => { });
        DeleteMedicineCommand = new RelayCommand<MedicineDto>(_ => { });
        PrescribeNowCommand = new RelayCommand<MedicineDto>(_ => { });
        SelectFormChipCommand = new RelayCommand<string>(_ => { });
        SelectStrengthChipCommand = new RelayCommand<string>(_ => { });
    }

    public MedicinesViewModel(
        IMedicineService medicineService,
        IDialogService dialogService,
        INavigationService navigationService,
        ILogger<MedicinesViewModel>? logger = null,
        IMedicineSearchService? medicineSearchService = null)
    {
        _medicineService = medicineService;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _logger = logger;
        _medicineSearchService = medicineSearchService;

        RefreshCommand = new AsyncRelayCommand(() => LoadMedicinesAsync());
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
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        await LoadMedicinesAsync();
    }

    protected override void OnLayoutModeChanged(LayoutMode newMode)
    {
        OnPropertyChanged(nameof(DrawerWidth));
    }

    public async Task LoadMedicinesAsync()
    {
        if (_medicineService == null) return;

        try
        {
            IsBusy = true;
            BusyMessage = "Loading medicines catalog...";

            var list = await _medicineService.GetMedicinesPagedAsync(1, 2000);
            _allLoadedMedicines = list.ToList();
            TotalMedicinesCount = _allLoadedMedicines.Count;

            ApplyFilters();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load medicines");
            _dialogService?.ShowError("Catalog Error", "Unable to load medicines catalog from database.");
        }
        finally
        {
            IsBusy = false;
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
        var filtered = _allLoadedMedicines.AsEnumerable();

        // 1. Status Filter
        if (!ShowInactive)
        {
            filtered = filtered.Where(m => m.IsActive);
        }

        // 2. Dosage Form Filter
        if (!string.IsNullOrWhiteSpace(SelectedFormFilter) && SelectedFormFilter != "All Forms")
        {
            filtered = SelectedFormFilter switch
            {
                "Drops" => filtered.Where(m => m.Form.Contains("Drop", StringComparison.OrdinalIgnoreCase)),
                "Cream / Ointment" => filtered.Where(m => m.Form.Contains("Cream", StringComparison.OrdinalIgnoreCase) || m.Form.Contains("Ointment", StringComparison.OrdinalIgnoreCase)),
                _ => filtered.Where(m => m.Form.Equals(SelectedFormFilter, StringComparison.OrdinalIgnoreCase))
            };
        }

        // 3. Search Query
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim();
            filtered = filtered.Where(m =>
                m.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(m.GenericName) && m.GenericName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                m.Strength.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.Form.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        // Sort by UsageCount desc, then Name asc
        var sorted = filtered.OrderByDescending(m => m.UsageCount)
                             .ThenBy(m => m.Name)
                             .ToList();

        Medicines.Clear();
        foreach (var item in sorted)
        {
            Medicines.Add(item);
        }

        FilteredMedicinesCount = Medicines.Count;
    }

    private void OpenAddDrawer()
    {
        IsEditing = false;
        _editingMedicineId = 0;
        FormName = string.Empty;
        FormGenericName = string.Empty;
        FormDosageForm = "Tablet";
        FormStrength = string.Empty;
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
        FormIsActive = dto.IsActive;
        FormErrorMessage = null;
        IsDrawerOpen = true;
    }

    private void CloseDrawer()
    {
        IsDrawerOpen = false;
        FormErrorMessage = null;
    }

    private bool ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            FormErrorMessage = "Medicine brand or name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(FormDosageForm))
        {
            FormErrorMessage = "Dosage form (e.g. Tablet, Syrup, Injection) is required.";
            return false;
        }

        FormErrorMessage = null;
        return true;
    }

    private async Task SaveMedicineAsync()
    {
        if (!ValidateForm() || _medicineService == null) return;

        try
        {
            IsBusy = true;
            BusyMessage = IsEditing ? "Updating medicine..." : "Saving new medicine...";

            if (IsEditing)
            {
                var updateDto = new UpdateMedicineDto
                {
                    Id = _editingMedicineId,
                    Name = FormName.Trim(),
                    GenericName = string.IsNullOrWhiteSpace(FormGenericName) ? null : FormGenericName.Trim(),
                    Form = FormDosageForm.Trim(),
                    Strength = FormStrength?.Trim() ?? string.Empty,
                    IsActive = FormIsActive
                };

                var result = await _medicineService.UpdateMedicineAsync(updateDto);
                if (result.IsSuccess)
                {
                    _dialogService?.ShowInformation("Medicine Updated", $"'{FormName}' has been updated in the catalog.");
                    CloseDrawer();
                    await LoadMedicinesAsync();
                }
                else if (result.ErrorMessage != null && result.ErrorMessage.Contains("DUPLICATE_WARNING", StringComparison.OrdinalIgnoreCase))
                {
                    var warningMessage = result.ErrorMessage.Replace("DUPLICATE_WARNING:", "").Trim();
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
                            await LoadMedicinesAsync();
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
                    Strength = FormStrength?.Trim() ?? string.Empty
                };

                var result = await _medicineService.CreateMedicineAsync(createDto);
                if (result.IsSuccess)
                {
                    _dialogService?.ShowInformation("Medicine Added", $"'{FormName}' is now available for prescribing.");
                    CloseDrawer();
                    await LoadMedicinesAsync();
                }
                else if (result.ErrorMessage != null && result.ErrorMessage.Contains("DUPLICATE_WARNING", StringComparison.OrdinalIgnoreCase))
                {
                    var warningMessage = result.ErrorMessage.Replace("DUPLICATE_WARNING:", "").Trim();
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
                            await LoadMedicinesAsync();
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
                await LoadMedicinesAsync();
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
        string message = summary.IsReferencedInPrescriptions
            ? $"'{dto.DisplayText}' is referenced in {summary.PrescriptionReferenceCount} historical prescription(s).\n\nIt will be safely deactivated from the catalog and hidden from future prescribing. Historical prescriptions will remain completely intact.\n\nProceed with deactivation?"
            : $"Are you sure you want to remove '{dto.DisplayText}' from the catalog?\n\nProceed?";

        var confirmed = _dialogService?.ShowConfirmation("Delete Medicine", message) ?? false;
        if (!confirmed) return;

        try
        {
            var result = await _medicineService.DeleteMedicineAsync(dto.Id);
            if (result.IsSuccess)
            {
                await LoadMedicinesAsync();
            }
            else
            {
                _dialogService?.ShowError("Delete Failed", result.ErrorMessage ?? "Could not delete medicine.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete medicine #{Id}", dto.Id);
        }
    }

    private void PrescribeNow(MedicineDto? dto)
    {
        if (dto == null || _navigationService == null) return;

        // Navigate to New Prescription pre-loaded with the selected catalog medicine
        _navigationService.NavigateTo(NavigationDestination.NewPrescription, dto);
    }
}
