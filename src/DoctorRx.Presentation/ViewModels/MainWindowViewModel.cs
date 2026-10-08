using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Presentation.Services;

namespace DoctorRx.Presentation.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IDoctorService _doctorService;
    private readonly IDialogService _dialogService;
    private readonly IDraftService _draftService;
    private ViewModelBase? _currentView;
    private string _currentViewTitle = "Dashboard";
    private DoctorDto? _activeDoctor;
    private NavigationDestination _selectedDestination = NavigationDestination.Dashboard;

    // Doctor profile setup modal/drawer
    private bool _isDoctorSetupOpen;
    private string _doctorSetupName = string.Empty;
    private string _doctorSetupQualification = string.Empty;
    private string _doctorSetupRegistrationNumber = string.Empty;
    private string _doctorSetupSpecialization = string.Empty;
    private string _doctorSetupClinicName = string.Empty;
    private string? _doctorSetupClinicAddress;
    private string? _doctorSetupPhone;
    private string? _doctorSetupEmail;
    private string? _doctorSetupClinicPhone;
    private string? _doctorSetupHeaderText;
    private string? _doctorSetupFooterText;
    private string? _doctorSetupErrorMessage;

    public ViewModelBase? CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    public string CurrentViewTitle
    {
        get => _currentViewTitle;
        private set => SetProperty(ref _currentViewTitle, value);
    }

    public DoctorDto? ActiveDoctor
    {
        get => _activeDoctor;
        private set
        {
            if (SetProperty(ref _activeDoctor, value))
            {
                OnPropertyChanged(nameof(HasActiveDoctor));
                OnPropertyChanged(nameof(DoctorBannerText));
            }
        }
    }

    public bool HasActiveDoctor => ActiveDoctor != null;
    public string DoctorBannerText => HasActiveDoctor 
        ? string.Empty 
        : "Doctor profile not set up. Please set up your physician & clinic details to enable prescription issuing.";

    public NavigationDestination SelectedDestination
    {
        get => _selectedDestination;
        set => SetProperty(ref _selectedDestination, value);
    }

    public string CurrentDateFormatted => DateTime.Today.ToString("dddd, dd MMMM yyyy");

    // Doctor Setup Form Properties
    public bool IsDoctorSetupOpen
    {
        get => _isDoctorSetupOpen;
        set => SetProperty(ref _isDoctorSetupOpen, value);
    }

    public string DoctorSetupName
    {
        get => _doctorSetupName;
        set => SetProperty(ref _doctorSetupName, value);
    }

    public string DoctorSetupQualification
    {
        get => _doctorSetupQualification;
        set => SetProperty(ref _doctorSetupQualification, value);
    }

    public string DoctorSetupRegistrationNumber
    {
        get => _doctorSetupRegistrationNumber;
        set => SetProperty(ref _doctorSetupRegistrationNumber, value);
    }

    public string DoctorSetupSpecialization
    {
        get => _doctorSetupSpecialization;
        set => SetProperty(ref _doctorSetupSpecialization, value);
    }

    public string DoctorSetupClinicName
    {
        get => _doctorSetupClinicName;
        set => SetProperty(ref _doctorSetupClinicName, value);
    }

    public string? DoctorSetupClinicAddress
    {
        get => _doctorSetupClinicAddress;
        set => SetProperty(ref _doctorSetupClinicAddress, value);
    }

    public string? DoctorSetupPhone
    {
        get => _doctorSetupPhone;
        set => SetProperty(ref _doctorSetupPhone, value);
    }

    public string? DoctorSetupEmail
    {
        get => _doctorSetupEmail;
        set => SetProperty(ref _doctorSetupEmail, value);
    }

    public string? DoctorSetupClinicPhone
    {
        get => _doctorSetupClinicPhone;
        set => SetProperty(ref _doctorSetupClinicPhone, value);
    }

    public string? DoctorSetupHeaderText
    {
        get => _doctorSetupHeaderText;
        set => SetProperty(ref _doctorSetupHeaderText, value);
    }

    public string? DoctorSetupFooterText
    {
        get => _doctorSetupFooterText;
        set => SetProperty(ref _doctorSetupFooterText, value);
    }

    public string? DoctorSetupErrorMessage
    {
        get => _doctorSetupErrorMessage;
        set
        {
            if (SetProperty(ref _doctorSetupErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasDoctorSetupError));
            }
        }
    }

    public bool HasDoctorSetupError => !string.IsNullOrWhiteSpace(DoctorSetupErrorMessage);

    public ICommand NavigateToDashboardCommand { get; }
    public ICommand NavigateToNewPrescriptionCommand { get; }
    public ICommand NavigateToPatientsCommand { get; }
    public ICommand NavigateToHistoryCommand { get; }
    public ICommand NavigateToMedicinesCommand { get; }
    public ICommand NavigateToSettingsCommand { get; }

    public ICommand OpenDoctorSetupCommand { get; }
    public ICommand CloseDoctorSetupCommand { get; }
    public ICommand SaveDoctorSetupCommand { get; }

    public MainWindowViewModel(INavigationService navigationService, IDoctorService doctorService, IDialogService dialogService, IDraftService draftService)
    {
        _navigationService = navigationService;
        _doctorService = doctorService;
        _dialogService = dialogService;
        _draftService = draftService;

        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;

        NavigateToDashboardCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.Dashboard));
        NavigateToNewPrescriptionCommand = new RelayCommand(() =>
        {
            if (!HasActiveDoctor)
            {
                _dialogService.ShowWarning("Doctor Profile Required", "Please configure your doctor profile before creating prescriptions.");
                OpenDoctorSetup();
                return;
            }
            _navigationService.NavigateTo(NavigationDestination.NewPrescription);
        });
        NavigateToPatientsCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.Patients));
        NavigateToHistoryCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.PrescriptionHistory));
        NavigateToMedicinesCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.Medicines));
        NavigateToSettingsCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.Settings));

        OpenDoctorSetupCommand = new RelayCommand(OpenDoctorSetup);
        CloseDoctorSetupCommand = new RelayCommand(() => IsDoctorSetupOpen = false);
        SaveDoctorSetupCommand = new AsyncRelayCommand(SaveDoctorSetupAsync);
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        ActiveDoctor = await _doctorService.GetActiveDoctorAsync();
        _navigationService.NavigateTo(NavigationDestination.Dashboard);

        if (HasActiveDoctor)
        {
            var draftCount = await _draftService.GetCountAsync();
            if (draftCount > 0)
            {
                var prompt = _dialogService.ShowConfirmation(
                    "Recover In-Progress Drafts",
                    $"There is {draftCount} unfinalized prescription draft(s) saved from a previous session.\n\nWould you like to resume editing your draft now?");
                if (prompt)
                {
                    var drafts = await _draftService.ListAsync();
                    if (drafts.Count > 0)
                    {
                        _navigationService.NavigateTo(NavigationDestination.NewPrescription, drafts[0].DraftKey);
                    }
                }
            }
        }
    }

    public bool? ConfirmCloseWithUnsavedChanges()
    {
        return _dialogService.ShowConfirmationWithCancel(
            "Unsaved Prescription Draft",
            "You have an unfinalized prescription in progress.\n\n" +
            "• Click 'Yes' to save the draft and exit.\n" +
            "• Click 'No' to discard changes and exit.\n" +
            "• Click 'Cancel' to continue editing.");
    }

    private void OpenDoctorSetup()
    {
        if (ActiveDoctor != null)
        {
            DoctorSetupName = ActiveDoctor.Name;
            DoctorSetupQualification = ActiveDoctor.Qualification;
            DoctorSetupRegistrationNumber = ActiveDoctor.RegistrationNumber;
            DoctorSetupSpecialization = ActiveDoctor.Specialization;
            DoctorSetupClinicName = ActiveDoctor.ClinicName;
            DoctorSetupClinicAddress = ActiveDoctor.ClinicAddress;
            DoctorSetupPhone = ActiveDoctor.Phone;
            DoctorSetupEmail = ActiveDoctor.Email;
            DoctorSetupClinicPhone = ActiveDoctor.ClinicPhone;
            DoctorSetupHeaderText = ActiveDoctor.HeaderText;
            DoctorSetupFooterText = ActiveDoctor.FooterText;
        }
        else
        {
            DoctorSetupName = string.Empty;
            DoctorSetupQualification = string.Empty;
            DoctorSetupRegistrationNumber = string.Empty;
            DoctorSetupSpecialization = string.Empty;
            DoctorSetupClinicName = string.Empty;
            DoctorSetupClinicAddress = string.Empty;
            DoctorSetupPhone = string.Empty;
            DoctorSetupEmail = string.Empty;
            DoctorSetupClinicPhone = string.Empty;
            DoctorSetupHeaderText = string.Empty;
            DoctorSetupFooterText = string.Empty;
        }

        DoctorSetupErrorMessage = null;
        IsDoctorSetupOpen = true;
    }

    private async Task SaveDoctorSetupAsync()
    {
        DoctorSetupErrorMessage = null;

        if (string.IsNullOrWhiteSpace(DoctorSetupName))
        {
            DoctorSetupErrorMessage = "Doctor Name is required.";
            return;
        }
        if (string.IsNullOrWhiteSpace(DoctorSetupQualification))
        {
            DoctorSetupErrorMessage = "Qualification is required (e.g., MBBS, FCPS).";
            return;
        }
        if (string.IsNullOrWhiteSpace(DoctorSetupRegistrationNumber))
        {
            DoctorSetupErrorMessage = "Medical Registration Number is required.";
            return;
        }
        if (string.IsNullOrWhiteSpace(DoctorSetupSpecialization))
        {
            DoctorSetupErrorMessage = "Specialization is required.";
            return;
        }
        if (string.IsNullOrWhiteSpace(DoctorSetupClinicName))
        {
            DoctorSetupErrorMessage = "Clinic Name is required.";
            return;
        }

        var dto = new CreateDoctorDto
        {
            Name = DoctorSetupName,
            Qualification = DoctorSetupQualification,
            RegistrationNumber = DoctorSetupRegistrationNumber,
            Specialization = DoctorSetupSpecialization,
            ClinicName = DoctorSetupClinicName,
            ClinicAddress = DoctorSetupClinicAddress,
            Phone = DoctorSetupPhone,
            Email = DoctorSetupEmail,
            ClinicPhone = DoctorSetupClinicPhone,
            HeaderText = DoctorSetupHeaderText,
            FooterText = DoctorSetupFooterText
        };

        var result = await _doctorService.CreateDoctorAsync(dto);
        if (result.IsSuccess && result.Value != null)
        {
            ActiveDoctor = result.Value;
            IsDoctorSetupOpen = false;
            _dialogService.ShowInformation("Profile Saved", $"Doctor profile for '{result.Value.Name}' has been successfully established.");
        }
        else
        {
            DoctorSetupErrorMessage = result.ErrorMessage ?? "Failed to save doctor profile.";
        }
    }

    private void OnCurrentViewModelChanged(ViewModelBase viewModel)
    {
        CurrentView = viewModel;
        SelectedDestination = _navigationService.CurrentDestination;

        CurrentViewTitle = _navigationService.CurrentDestination switch
        {
            NavigationDestination.Dashboard => "Clinic Overview & Dashboard",
            NavigationDestination.NewPrescription => "Create New Prescription",
            NavigationDestination.Patients => "Patient Directory & Records",
            NavigationDestination.PrescriptionHistory => "Prescription Archive & History",
            NavigationDestination.Medicines => "Medicine Catalog & Formulations",
            NavigationDestination.Settings => "Clinic & Application Settings",
            _ => "DoctorRx"
        };
    }
}
