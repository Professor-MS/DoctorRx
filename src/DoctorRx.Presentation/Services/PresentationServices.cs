using System;
using System.Windows;
using DoctorRx.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DoctorRx.Presentation.Services;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly System.Collections.Generic.Stack<NavigationDestination> _history = new();
    private ViewModelBase? _currentViewModel;
    private NavigationDestination _currentDestination = NavigationDestination.Dashboard;
    private NavigationDestination? _previousDestination;

    public ViewModelBase? CurrentViewModel => _currentViewModel;
    public NavigationDestination CurrentDestination => _currentDestination;
    public NavigationDestination? PreviousDestination => _previousDestination;

    public event Action<ViewModelBase>? CurrentViewModelChanged;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo(NavigationDestination destination, object? parameter = null)
    {
        if (destination != _currentDestination)
        {
            _previousDestination = _currentDestination;
            _history.Push(_currentDestination);
        }
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

    public void GoBack()
    {
        if (_history.Count > 0)
        {
            var prev = _history.Pop();
            _previousDestination = _currentDestination;
            _currentDestination = prev;

            ViewModelBase nextViewModel = prev switch
            {
                NavigationDestination.Dashboard => _serviceProvider.GetRequiredService<DashboardViewModel>(),
                NavigationDestination.Patients => _serviceProvider.GetRequiredService<PatientsViewModel>(),
                NavigationDestination.NewPrescription => _serviceProvider.GetRequiredService<NewPrescriptionViewModel>(),
                NavigationDestination.PrescriptionHistory => _serviceProvider.GetRequiredService<PrescriptionHistoryViewModel>(),
                NavigationDestination.Medicines => _serviceProvider.GetRequiredService<MedicinesViewModel>(),
                NavigationDestination.Settings => _serviceProvider.GetRequiredService<SettingsViewModel>(),
                NavigationDestination.PrescriptionDetail => _serviceProvider.GetRequiredService<PrescriptionDetailViewModel>(),
                _ => throw new ArgumentOutOfRangeException(nameof(prev), prev, null)
            };

            _currentViewModel = nextViewModel;
            _ = nextViewModel.InitializeAsync(null);
            CurrentViewModelChanged?.Invoke(nextViewModel);
        }
    }
}

public class DialogService : IDialogService
{
    private static void RunOnUi(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(action);
            return;
        }
        action();
    }

    private static T RunOnUi<T>(Func<T> action)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            return app.Dispatcher.Invoke(action);
        }
        return action();
    }

    public void ShowInformation(string title, string message)
    {
        RunOnUi(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new Views.CustomDialogWindow(title, message, Views.CustomDialogWindow.DialogType.Information);
            dialog.ShowDialog();
        });
    }

    public void ShowWarning(string title, string message)
    {
        RunOnUi(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dialog = new Views.CustomDialogWindow(title, message, Views.CustomDialogWindow.DialogType.Warning);
            dialog.ShowDialog();
        });
    }

    public void ShowError(string title, string message)
    {
        RunOnUi(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var dialog = new Views.CustomDialogWindow(title, message, Views.CustomDialogWindow.DialogType.Error);
            dialog.ShowDialog();
        });
    }

    public bool ShowConfirmation(string title, string message) => ShowConfirmation(title, message, "Yes", "No");

    public bool ShowConfirmation(string title, string message, string confirmText, string cancelText)
    {
        return RunOnUi(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            }
            var dialog = new Views.CustomDialogWindow(title, message, Views.CustomDialogWindow.DialogType.Confirmation, null, confirmText, cancelText);
            dialog.ShowDialog();
            return dialog.DialogBooleanResult == true;
        });
    }

    public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel")
    {
        return RunOnUi(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                var res = MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                return res switch
                {
                    MessageBoxResult.Yes => true,
                    MessageBoxResult.No => false,
                    _ => null
                };
            }
            var dialog = new Views.CustomDialogWindow(title, message, Views.CustomDialogWindow.DialogType.ConfirmationWithCancel, null, yesText, noText, cancelText);
            dialog.ShowDialog();
            return dialog.DialogBooleanResult;
        });
    }
}
