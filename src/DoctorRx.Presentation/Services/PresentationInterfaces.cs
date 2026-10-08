using System;
using DoctorRx.Presentation.ViewModels;

namespace DoctorRx.Presentation.Services;

public enum NavigationDestination
{
    Dashboard,
    NewPrescription,
    Patients,
    PrescriptionHistory,
    Medicines,
    Settings,
    PrescriptionDetail
}

public interface INavigationService
{
    ViewModelBase? CurrentViewModel { get; }
    NavigationDestination CurrentDestination { get; }
    event Action<ViewModelBase>? CurrentViewModelChanged;

    void NavigateTo(NavigationDestination destination, object? parameter = null);
}

public interface IDialogService
{
    void ShowInformation(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
    bool ShowConfirmation(string title, string message);
    bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel");
}
