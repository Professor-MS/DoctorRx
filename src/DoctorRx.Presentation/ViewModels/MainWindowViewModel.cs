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
    private ViewModelBase? _currentView;
    private string _currentViewTitle = "Dashboard";
    private DoctorDto? _activeDoctor;
    private NavigationDestination _selectedDestination = NavigationDestination.Dashboard;

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
        private set => SetProperty(ref _activeDoctor, value);
    }

    public NavigationDestination SelectedDestination
    {
        get => _selectedDestination;
        set => SetProperty(ref _selectedDestination, value);
    }

    public string CurrentDateFormatted => DateTime.Today.ToString("dddd, dd MMMM yyyy");

    public ICommand NavigateToDashboardCommand { get; }
    public ICommand NavigateToNewPrescriptionCommand { get; }
    public ICommand NavigateToPatientsCommand { get; }
    public ICommand NavigateToHistoryCommand { get; }
    public ICommand NavigateToMedicinesCommand { get; }
    public ICommand NavigateToSettingsCommand { get; }

    public MainWindowViewModel(INavigationService navigationService, IDoctorService doctorService)
    {
        _navigationService = navigationService;
        _doctorService = doctorService;

        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;

        NavigateToDashboardCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.Dashboard));
        NavigateToNewPrescriptionCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.NewPrescription));
        NavigateToPatientsCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.Patients));
        NavigateToHistoryCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.PrescriptionHistory));
        NavigateToMedicinesCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.Medicines));
        NavigateToSettingsCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.Settings));
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        ActiveDoctor = await _doctorService.GetActiveDoctorAsync();
        _navigationService.NavigateTo(NavigationDestination.Dashboard);
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
