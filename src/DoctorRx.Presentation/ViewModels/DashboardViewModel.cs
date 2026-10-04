using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Presentation.Services;

namespace DoctorRx.Presentation.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly IDashboardService _dashboardService;
    private readonly INavigationService _navigationService;

    private int _totalPatients;
    private int _prescriptionsToday;
    private int _totalPrescriptions;
    private int _totalMedicinesInCatalog;

    public int TotalPatients
    {
        get => _totalPatients;
        set => SetProperty(ref _totalPatients, value);
    }

    public int PrescriptionsToday
    {
        get => _prescriptionsToday;
        set => SetProperty(ref _prescriptionsToday, value);
    }

    public int TotalPrescriptions
    {
        get => _totalPrescriptions;
        set => SetProperty(ref _totalPrescriptions, value);
    }

    public int TotalMedicinesInCatalog
    {
        get => _totalMedicinesInCatalog;
        set => SetProperty(ref _totalMedicinesInCatalog, value);
    }

    public ObservableCollection<PrescriptionSummaryDto> RecentPrescriptions { get; } = new();
    public ObservableCollection<PatientDto> RecentPatients { get; } = new();

    public ICommand NewPrescriptionCommand { get; }
    public ICommand ViewAllPatientsCommand { get; }
    public ICommand ViewAllPrescriptionsCommand { get; }
    public ICommand RefreshCommand { get; }

    public DashboardViewModel(IDashboardService dashboardService, INavigationService navigationService)
    {
        _dashboardService = dashboardService;
        _navigationService = navigationService;

        NewPrescriptionCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.NewPrescription));
        ViewAllPatientsCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.Patients));
        ViewAllPrescriptionsCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.PrescriptionHistory));
        RefreshCommand = new AsyncRelayCommand(LoadStatsAsync);
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        await LoadStatsAsync();
    }

    private async Task LoadStatsAsync()
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Loading clinic metrics...";

            var stats = await _dashboardService.GetDashboardStatsAsync();

            TotalPatients = stats.TotalPatients;
            PrescriptionsToday = stats.PrescriptionsToday;
            TotalPrescriptions = stats.TotalPrescriptions;
            TotalMedicinesInCatalog = stats.TotalMedicinesInCatalog;

            RecentPrescriptions.Clear();
            foreach (var rx in stats.RecentPrescriptions)
            {
                RecentPrescriptions.Add(rx);
            }

            RecentPatients.Clear();
            foreach (var pt in stats.RecentPatients)
            {
                RecentPatients.Add(pt);
            }
        }
        catch (Exception)
        {
            // Logging handled at service level
        }
        finally
        {
            IsBusy = false;
        }
    }
}
