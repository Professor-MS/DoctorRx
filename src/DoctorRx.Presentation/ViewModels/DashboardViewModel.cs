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
    private readonly IDraftService _draftService;

    private int _totalPatients;
    private int _prescriptionsToday;
    private int _totalPrescriptions;
    private int _totalMedicinesInCatalog;
    private int _activeDraftsCount;

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

    public int ActiveDraftsCount
    {
        get => _activeDraftsCount;
        set
        {
            if (SetProperty(ref _activeDraftsCount, value))
            {
                OnPropertyChanged(nameof(HasActiveDrafts));
            }
        }
    }

    private double _availableWidth;

    public double AvailableWidth
    {
        get => _availableWidth;
        set
        {
            if (SetProperty(ref _availableWidth, value))
            {
                OnPropertyChanged(nameof(StatCardColumns));
            }
        }
    }

    public int StatCardColumns
    {
        get
        {
            if (AvailableWidth > 0 && AvailableWidth < 480) return 1;
            if (IsWide) return 4;
            return 2;
        }
    }

    protected override void OnLayoutModeChanged(LayoutMode newMode)
    {
        base.OnLayoutModeChanged(newMode);
        OnPropertyChanged(nameof(StatCardColumns));
    }

    public bool HasActiveDrafts => ActiveDraftsCount > 0;

    public ObservableCollection<PrescriptionSummaryDto> RecentPrescriptions { get; } = new();
    public ObservableCollection<PatientDto> RecentPatients { get; } = new();
    public ObservableCollection<DraftSummaryDto> ActiveDrafts { get; } = new();

    public ICommand NewPrescriptionCommand { get; }
    public ICommand ViewAllPatientsCommand { get; }
    public ICommand ViewAllPrescriptionsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ResumeDraftCommand { get; }
    public ICommand DiscardDraftCommand { get; }

    public DashboardViewModel(IDashboardService dashboardService, INavigationService navigationService, IDraftService draftService)
    {
        _dashboardService = dashboardService;
        _navigationService = navigationService;
        _draftService = draftService;

        NewPrescriptionCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.NewPrescription));
        ViewAllPatientsCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.Patients));
        ViewAllPrescriptionsCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.PrescriptionHistory));
        RefreshCommand = new AsyncRelayCommand(LoadStatsAsync);

        ResumeDraftCommand = new RelayCommand<DraftSummaryDto>(draft =>
        {
            if (draft != null)
            {
                _navigationService.NavigateTo(NavigationDestination.NewPrescription, draft.DraftKey);
            }
        });

        DiscardDraftCommand = new AsyncRelayCommand<DraftSummaryDto>(async draft =>
        {
            if (draft != null)
            {
                await _draftService.DiscardAsync(draft.DraftKey);
                await LoadStatsAsync();
            }
        });
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

            var drafts = await _draftService.ListAsync();
            ActiveDrafts.Clear();
            foreach (var d in drafts)
            {
                ActiveDrafts.Add(d);
            }
            ActiveDraftsCount = drafts.Count;
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
