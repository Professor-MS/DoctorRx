using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Enums;
using DoctorRx.Presentation.Services;

namespace DoctorRx.Presentation.ViewModels;

public class PatientsViewModel : ViewModelBase
{
    private readonly IPatientService _patientService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private string _searchQuery = string.Empty;
    private PatientDto? _selectedPatient;
    private bool _isDrawerOpen;
    private bool _isEditing;

    // Form fields
    private int _editingPatientId;
    private string _formName = string.Empty;
    private int? _formAge;
    private Gender _formGender = Gender.Male;
    private string? _formPhone;
    private string? _formAddress;
    private string? _formMedicalHistory;
    private string? _formKnownAllergies;
    private string? _formErrorMessage;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _ = SearchPatientsAsync();
            }
        }
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
    public ICommand ClearSearchCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand CreatePrescriptionForPatientCommand { get; }

    public PatientsViewModel(
        IPatientService patientService,
        IDialogService dialogService,
        INavigationService navigationService)
    {
        _patientService = patientService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        OpenNewPatientDrawerCommand = new RelayCommand(OpenNewPatientDrawer);
        EditPatientCommand = new RelayCommand<PatientDto>(OpenEditPatientDrawer);
        CloseDrawerCommand = new RelayCommand(CloseDrawer);
        SavePatientCommand = new AsyncRelayCommand(SavePatientAsync);
        DeletePatientCommand = new AsyncRelayCommand<PatientDto>(DeletePatientAsync);
        ClearSearchCommand = new RelayCommand(ClearSearch);
        RefreshCommand = new AsyncRelayCommand(LoadPatientsAsync);
        CreatePrescriptionForPatientCommand = new RelayCommand<PatientDto>(CreatePrescriptionForPatient);
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        await LoadPatientsAsync();
    }

    private async Task LoadPatientsAsync()
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Loading patient records...";

            var list = await _patientService.GetAllPatientsAsync();
            Patients.Clear();
            foreach (var p in list)
            {
                Patients.Add(p);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SearchPatientsAsync()
    {
        try
        {
            var results = await _patientService.SearchPatientsAsync(SearchQuery);
            Patients.Clear();
            foreach (var p in results)
            {
                Patients.Add(p);
            }
        }
        catch (Exception)
        {
            // Handled
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

            if (IsEditing)
            {
                var updateDto = new UpdatePatientDto
                {
                    Id = _editingPatientId,
                    Name = FormName,
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
                    await LoadPatientsAsync();
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
                    _dialogService.ShowInformation("Patient Registered", $"Patient '{result.Value?.Name}' (MRN #{result.Value?.Id}) has been registered.");
                    CloseDrawer();
                    await LoadPatientsAsync();
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

    private async Task DeletePatientAsync(PatientDto? patient)
    {
        if (patient == null) return;

        bool confirm = _dialogService.ShowConfirmation(
            "Confirm Delete",
            $"Are you sure you want to delete patient '{patient.Name}' (MRN #{patient.Id})? This action cannot be undone.");

        if (!confirm) return;

        var result = await _patientService.DeletePatientAsync(patient.Id);
        if (result.IsSuccess)
        {
            _dialogService.ShowInformation("Patient Removed", $"Patient '{patient.Name}' has been removed.");
            await LoadPatientsAsync();
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
