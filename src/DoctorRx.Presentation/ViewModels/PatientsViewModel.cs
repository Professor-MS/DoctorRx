using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Enums;
using DoctorRx.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Presentation.ViewModels;

public class PatientsViewModel : ViewModelBase
{
    private readonly IPatientService _patientService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<PatientsViewModel> _logger;

    private const int PageSize = 50;
    private int _currentPage = 1;
    private int _totalPatientsCount;
    private bool _hasMorePatients;
    private bool _isLoadingMore;

    private string _searchQuery = string.Empty;
    private CancellationTokenSource? _searchCts;
    private string? _searchErrorMessage;

    private PatientDto? _selectedPatient;
    private bool _isDrawerOpen;
    private bool _isEditing;

    private bool _showArchived;

    // Form fields
    private int _editingPatientId;
    private string _formName = string.Empty;
    private DateTime? _formDateOfBirth;
    private int? _formAge;
    private Gender _formGender = Gender.Male;
    private string? _formPhone;
    private string? _formAddress;
    private string? _formMedicalHistory;
    private string? _formKnownAllergies;
    private string? _formErrorMessage;

    public bool ShowArchived
    {
        get => _showArchived;
        set
        {
            if (SetProperty(ref _showArchived, value))
            {
                _ = LoadPatientsAsync(reset: true);
            }
        }
    }

    public DateTime? FormDateOfBirth
    {
        get => _formDateOfBirth;
        set
        {
            if (SetProperty(ref _formDateOfBirth, value))
            {
                if (value.HasValue)
                {
                    var today = DateTime.Today;
                    var age = today.Year - value.Value.Year;
                    if (value.Value.Date > today.AddYears(-age)) age--;
                    FormAge = Math.Max(0, age);
                }
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                OnPropertyChanged(nameof(HasSearchQuery));
                TriggerDebouncedSearch();
            }
        }
    }

    public string? SearchErrorMessage
    {
        get => _searchErrorMessage;
        set
        {
            if (SetProperty(ref _searchErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasSearchError));
            }
        }
    }

    public bool HasSearchError => !string.IsNullOrWhiteSpace(SearchErrorMessage);

    public int TotalPatientsCount
    {
        get => _totalPatientsCount;
        set => SetProperty(ref _totalPatientsCount, value);
    }

    public bool HasMorePatients
    {
        get => _hasMorePatients;
        set => SetProperty(ref _hasMorePatients, value);
    }

    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        set => SetProperty(ref _isLoadingMore, value);
    }

    public PatientDto? SelectedPatient
    {
        get => _selectedPatient;
        set => SetProperty(ref _selectedPatient, value);
    }

    public bool IsDrawerOpen
    {
        get => _isDrawerOpen;
        set => SetProperty(ref _isDrawerOpen, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string DrawerTitle => IsEditing ? "Edit Patient Details" : "Register New Patient";

    public string FormName
    {
        get => _formName;
        set => SetProperty(ref _formName, value);
    }

    public int? FormAge
    {
        get => _formAge;
        set => SetProperty(ref _formAge, value);
    }

    public Gender FormGender
    {
        get => _formGender;
        set => SetProperty(ref _formGender, value);
    }

    public string? FormPhone
    {
        get => _formPhone;
        set => SetProperty(ref _formPhone, value);
    }

    public string? FormAddress
    {
        get => _formAddress;
        set => SetProperty(ref _formAddress, value);
    }

    public string? FormMedicalHistory
    {
        get => _formMedicalHistory;
        set => SetProperty(ref _formMedicalHistory, value);
    }

    public string? FormKnownAllergies
    {
        get => _formKnownAllergies;
        set => SetProperty(ref _formKnownAllergies, value);
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
    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

    public ObservableCollection<PatientDto> Patients { get; } = new();
    public ObservableCollection<Gender> AvailableGenders { get; } = new(Enum.GetValues<Gender>());

    public ICommand OpenNewPatientDrawerCommand { get; }
    public ICommand EditPatientCommand { get; }
    public ICommand CloseDrawerCommand { get; }
    public ICommand SavePatientCommand { get; }
    public ICommand DeletePatientCommand { get; }
    public ICommand ArchivePatientCommand { get; }
    public ICommand RestorePatientCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand LoadMorePatientsCommand { get; }
    public ICommand CreatePrescriptionForPatientCommand { get; }

    public PatientsViewModel(
        IPatientService patientService,
        IDialogService dialogService,
        INavigationService navigationService,
        ILogger<PatientsViewModel> logger)
    {
        _patientService = patientService;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _logger = logger;

        OpenNewPatientDrawerCommand = new RelayCommand(OpenNewPatientDrawer);
        EditPatientCommand = new RelayCommand<PatientDto>(OpenEditPatientDrawer);
        CloseDrawerCommand = new RelayCommand(CloseDrawer);
        SavePatientCommand = new AsyncRelayCommand(SavePatientAsync);
        DeletePatientCommand = new AsyncRelayCommand<PatientDto>(DeletePatientAsync);
        ArchivePatientCommand = new AsyncRelayCommand<PatientDto>(ArchivePatientAsync);
        RestorePatientCommand = new AsyncRelayCommand<PatientDto>(RestorePatientAsync);
        ClearSearchCommand = new RelayCommand(ClearSearch);
        RefreshCommand = new AsyncRelayCommand(() => LoadPatientsAsync(reset: true));
        LoadMorePatientsCommand = new AsyncRelayCommand(LoadMorePatientsAsync);
        CreatePrescriptionForPatientCommand = new RelayCommand<PatientDto>(CreatePrescriptionForPatient);
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        await LoadPatientsAsync(reset: true);
    }

    private void TriggerDebouncedSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (token.IsCancellationRequested) return;

                await SearchPatientsAsync(token);
            }
            catch (OperationCanceledException)
            {
                // Ignored - newer keystroke arrived
            }
        }, token);
    }

    private async Task LoadPatientsAsync(bool reset = true)
    {
        try
        {
            IsBusy = true;
            SearchErrorMessage = null;
            BusyMessage = "Loading patient records...";

            if (reset)
            {
                _currentPage = 1;
                Patients.Clear();
            }

            var paged = await _patientService.GetPatientsPagedAsync(_currentPage, PageSize, showArchived: ShowArchived);
            TotalPatientsCount = paged.TotalCount;
            HasMorePatients = paged.HasMore;

            foreach (var p in paged.Items)
            {
                Patients.Add(p);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load patient records (page {Page})", _currentPage);
            SearchErrorMessage = "Unable to load patient records. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadMorePatientsAsync()
    {
        if (IsLoadingMore || !HasMorePatients) return;

        try
        {
            IsLoadingMore = true;
            _currentPage++;

            var paged = await _patientService.GetPatientsPagedAsync(_currentPage, PageSize, showArchived: ShowArchived);
            TotalPatientsCount = paged.TotalCount;
            HasMorePatients = paged.HasMore;

            foreach (var p in paged.Items)
            {
                Patients.Add(p);
            }
        }
        catch (Exception ex)
        {
            _currentPage--;
            _logger.LogError(ex, "Failed to load additional patient records (page {Page})", _currentPage + 1);
            SearchErrorMessage = "Unable to load more patients. Please try again.";
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private async Task SearchPatientsAsync(CancellationToken cancellationToken)
    {
        try
        {
            SearchErrorMessage = null;

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                await LoadPatientsAsync(reset: true);
                return;
            }

            var results = await _patientService.SearchPatientsAsync(SearchQuery, maxResults: 50, showArchived: ShowArchived, cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;

            // Dispatch to UI collection
            Patients.Clear();
            foreach (var p in results)
            {
                Patients.Add(p);
            }
            HasMorePatients = false;
        }
        catch (OperationCanceledException)
        {
            // Ignored - superseded by newer query
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Patient search failed");
            SearchErrorMessage = "Search failed. Please try again.";
        }
    }

    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    private void OpenNewPatientDrawer()
    {
        IsEditing = false;
        _editingPatientId = 0;
        FormName = string.Empty;
        FormDateOfBirth = null;
        FormAge = null;
        FormGender = Gender.Male;
        FormPhone = string.Empty;
        FormAddress = string.Empty;
        FormMedicalHistory = string.Empty;
        FormKnownAllergies = string.Empty;
        FormErrorMessage = null;
        IsDrawerOpen = true;
    }

    private void OpenEditPatientDrawer(PatientDto? patient)
    {
        if (patient == null) return;

        IsEditing = true;
        _editingPatientId = patient.Id;
        FormName = patient.Name;
        FormDateOfBirth = patient.DateOfBirth.HasValue ? patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue) : null;
        FormAge = patient.Age;
        FormGender = patient.Gender;
        FormPhone = patient.Phone;
        FormAddress = patient.Address;
        FormMedicalHistory = patient.MedicalHistoryNotes;
        FormKnownAllergies = patient.KnownAllergies;
        FormErrorMessage = null;
        IsDrawerOpen = true;
    }

    private void CloseDrawer()
    {
        IsDrawerOpen = false;
        FormErrorMessage = null;
    }

    private async Task SavePatientAsync()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            FormErrorMessage = "Patient name is required.";
            return;
        }

        if (FormAge is < 0 or > 130)
        {
            FormErrorMessage = "Please enter a valid age between 0 and 130.";
            return;
        }

        try
        {
            IsBusy = true;
            FormErrorMessage = null;

            DateOnly? dob = FormDateOfBirth.HasValue ? DateOnly.FromDateTime(FormDateOfBirth.Value) : null;

            if (IsEditing)
            {
                var updateDto = new UpdatePatientDto
                {
                    Id = _editingPatientId,
                    Name = FormName,
                    DateOfBirth = dob,
                    Age = FormAge,
                    Gender = FormGender,
                    Phone = FormPhone,
                    Address = FormAddress,
                    MedicalHistoryNotes = FormMedicalHistory,
                    KnownAllergies = FormKnownAllergies
                };

                var result = await _patientService.UpdatePatientAsync(updateDto);
                if (result.IsSuccess)
                {
                    _dialogService.ShowInformation("Patient Updated", $"Details for '{result.Value?.Name}' updated successfully.");
                    CloseDrawer();
                    await LoadPatientsAsync(reset: true);
                }
                else
                {
                    FormErrorMessage = result.ErrorMessage;
                }
            }
            else
            {
                var createDto = new CreatePatientDto
                {
                    Name = FormName,
                    DateOfBirth = dob,
                    Age = FormAge,
                    Gender = FormGender,
                    Phone = FormPhone,
                    Address = FormAddress,
                    MedicalHistoryNotes = FormMedicalHistory,
                    KnownAllergies = FormKnownAllergies
                };

                var result = await _patientService.CreatePatientAsync(createDto);
                if (result.IsSuccess)
                {
                    _dialogService.ShowInformation("Patient Registered", $"Patient '{result.Value?.Name}' has been registered.");
                    CloseDrawer();
                    await LoadPatientsAsync(reset: true);
                }
                else if (result.ErrorMessage != null && result.ErrorMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                {
                    bool proceed = _dialogService.ShowConfirmation(
                        "Potential Duplicate Patient",
                        $"{result.ErrorMessage}\n\nDo you want to proceed and register this patient anyway?");

                    if (proceed)
                    {
                        var dupResult = await _patientService.CreatePatientAsync(createDto, allowDuplicate: true);
                        if (dupResult.IsSuccess)
                        {
                            _dialogService.ShowInformation("Patient Registered", $"Patient '{dupResult.Value?.Name}' registered.");
                            CloseDrawer();
                            await LoadPatientsAsync(reset: true);
                        }
                        else
                        {
                            FormErrorMessage = dupResult.ErrorMessage;
                        }
                    }
                    else
                    {
                        FormErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    FormErrorMessage = result.ErrorMessage;
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ArchivePatientAsync(PatientDto? patient)
    {
        if (patient == null) return;

        bool confirm = _dialogService.ShowConfirmation(
            "Archive Patient",
            $"Archive record for '{patient.Name}'? This patient will be hidden from default views.");

        if (!confirm) return;

        var result = await _patientService.ArchivePatientAsync(patient.Id);
        if (result.IsSuccess)
        {
            _dialogService.ShowInformation("Patient Archived", $"Patient '{patient.Name}' has been archived.");
            await LoadPatientsAsync(reset: true);
        }
        else
        {
            _dialogService.ShowWarning("Archive Failed", result.ErrorMessage ?? "Could not archive patient.");
        }
    }

    private async Task RestorePatientAsync(PatientDto? patient)
    {
        if (patient == null) return;

        var result = await _patientService.RestorePatientAsync(patient.Id);
        if (result.IsSuccess)
        {
            _dialogService.ShowInformation("Patient Restored", $"Patient '{patient.Name}' has been restored.");
            await LoadPatientsAsync(reset: true);
        }
        else
        {
            _dialogService.ShowWarning("Restore Failed", result.ErrorMessage ?? "Could not restore patient.");
        }
    }

    private async Task DeletePatientAsync(PatientDto? patient)
    {
        if (patient == null) return;

        bool confirm = _dialogService.ShowConfirmation(
            "Confirm Delete",
            $"Are you sure you want to delete patient '{patient.Name}'? This action cannot be undone.");

        if (!confirm) return;

        var result = await _patientService.DeletePatientAsync(patient.Id);
        if (result.IsSuccess)
        {
            _dialogService.ShowInformation("Patient Removed", $"Patient '{patient.Name}' has been removed.");
            await LoadPatientsAsync(reset: true);
        }
        else
        {
            _dialogService.ShowWarning("Cannot Delete Patient", result.ErrorMessage ?? "Patient could not be deleted.");
        }
    }

    private void CreatePrescriptionForPatient(PatientDto? patient)
    {
        if (patient == null) return;
        _navigationService.NavigateTo(NavigationDestination.NewPrescription, patient);
    }
}
