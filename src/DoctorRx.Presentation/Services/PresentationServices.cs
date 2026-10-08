using System;
using System.Windows;
using DoctorRx.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DoctorRx.Presentation.Services;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private ViewModelBase? _currentViewModel;
    private NavigationDestination _currentDestination = NavigationDestination.Dashboard;

    public ViewModelBase? CurrentViewModel => _currentViewModel;
    public NavigationDestination CurrentDestination => _currentDestination;

    public event Action<ViewModelBase>? CurrentViewModelChanged;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo(NavigationDestination destination, object? parameter = null)
    {
        _currentDestination = destination;

        ViewModelBase nextViewModel = destination switch
        {
            NavigationDestination.Dashboard => _serviceProvider.GetRequiredService<DashboardViewModel>(),
            NavigationDestination.Patients => _serviceProvider.GetRequiredService<PatientsViewModel>(),
            NavigationDestination.NewPrescription => _serviceProvider.GetRequiredService<NewPrescriptionViewModel>(),
            NavigationDestination.PrescriptionHistory => _serviceProvider.GetRequiredService<PrescriptionHistoryViewModel>(),
            NavigationDestination.Medicines => _serviceProvider.GetRequiredService<MedicinesViewModel>(),
            NavigationDestination.Settings => _serviceProvider.GetRequiredService<SettingsViewModel>(),
            NavigationDestination.PrescriptionDetail => _serviceProvider.GetRequiredService<PrescriptionDetailViewModel>(),
            _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, null)
        };

        _currentViewModel = nextViewModel;
        _ = nextViewModel.InitializeAsync(parameter);
        CurrentViewModelChanged?.Invoke(nextViewModel);
    }
}

public class DialogService : IDialogService
{
    public void ShowInformation(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool ShowConfirmation(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public bool? ShowConfirmationWithCancel(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return result switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null
        };
    }
}
