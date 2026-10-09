using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Enums;
using DoctorRx.Presentation.Services;

namespace DoctorRx.Presentation.ViewModels;

public class PrescriptionDetailViewModel : ViewModelBase
{
    public override NavigationSection NavigationSection => NavigationSection.PrescriptionHistory;

    private readonly IPrescriptionService _prescriptionService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private PrescriptionDetailDto? _prescription;
    private PrescriptionSummaryDto? _replacementPrescription;

    public NavigationDestination ReturnDestination { get; private set; } = NavigationDestination.PrescriptionHistory;

    public PrescriptionDetailDto? Prescription
    {
        get => _prescription;
        private set
        {
            if (SetProperty(ref _prescription, value))
            {
                OnPropertyChanged(nameof(CanAmendOrCancel));
                OnPropertyChanged(nameof(IsCancelled));
                OnPropertyChanged(nameof(IsSuperseded));
                OnPropertyChanged(nameof(IsFinalized));
                OnPropertyChanged(nameof(StatusBadgeText));
            }
        }
    }

    public PrescriptionSummaryDto? ReplacementPrescription
    {
        get => _replacementPrescription;
        private set
        {
            if (SetProperty(ref _replacementPrescription, value))
            {
                OnPropertyChanged(nameof(HasReplacementPrescription));
                OnPropertyChanged(nameof(StatusBadgeText));
            }
        }
    }

    public bool HasReplacementPrescription => ReplacementPrescription != null;

    public string StatusBadgeText => Prescription?.Status switch
    {
        PrescriptionStatus.Finalized => "Issued (locked)",
        PrescriptionStatus.Cancelled => "Cancelled",
        PrescriptionStatus.Superseded => ReplacementPrescription != null
            ? $"Replaced by {ReplacementPrescription.PrescriptionNumber}"
            : "Replaced",
        _ => Prescription?.Status.ToString() ?? string.Empty
    };

    public bool CanAmendOrCancel => Prescription?.Status == PrescriptionStatus.Finalized;
    public bool IsCancelled => Prescription?.Status == PrescriptionStatus.Cancelled;
    public bool IsSuperseded => Prescription?.Status == PrescriptionStatus.Superseded;
    public bool IsFinalized => Prescription?.Status == PrescriptionStatus.Finalized;

    public ICommand BackCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand EditPrescriptionCommand { get; }
    public ICommand AmendCommand => EditPrescriptionCommand;
    public ICommand OpenReplacementCommand { get; }

    public PrescriptionDetailViewModel(
        IPrescriptionService prescriptionService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _prescriptionService = prescriptionService;
        _navigationService = navigationService;
        _dialogService = dialogService;

        BackCommand = new RelayCommand(() => _navigationService.NavigateTo(ReturnDestination));
        PrintCommand = new RelayCommand(PrintPrescription);
        CancelCommand = new AsyncRelayCommand(CancelPrescriptionAsync);
        EditPrescriptionCommand = new RelayCommand(EditPrescription);
        OpenReplacementCommand = new RelayCommand(() =>
        {
            if (ReplacementPrescription != null)
            {
                _navigationService.NavigateTo(NavigationDestination.PrescriptionDetail, ReplacementPrescription.Id);
            }
        });
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        if (_navigationService.PreviousDestination == NavigationDestination.Patients)
        {
            ReturnDestination = NavigationDestination.Patients;
        }
        else if (_navigationService.PreviousDestination == NavigationDestination.Dashboard)
        {
            ReturnDestination = NavigationDestination.Dashboard;
        }
        else
        {
            ReturnDestination = NavigationDestination.PrescriptionHistory;
        }

        if (parameter is int prescriptionId)
        {
            Prescription = await _prescriptionService.GetPrescriptionByIdAsync(prescriptionId);
        }
        else if (parameter is PrescriptionDetailDto detail)
        {
            Prescription = detail;
        }

        if (Prescription != null && Prescription.Status == PrescriptionStatus.Superseded)
        {
            ReplacementPrescription = await _prescriptionService.GetReplacementPrescriptionAsync(Prescription.Id);
        }
        else
        {
            ReplacementPrescription = null;
        }
    }

    private void PrintPrescription()
    {
        if (Prescription == null) return;

        var app = System.Windows.Application.Current;
        if (app != null)
        {
            if (!app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.Invoke(() =>
                {
                    var preview = new Views.PrintPreviewWindow(Prescription);
                    preview.ShowDialog();
                });
                return;
            }

            var window = new Views.PrintPreviewWindow(Prescription);
            window.ShowDialog();
            return;
        }

        _dialogService.ShowInformation("Print Prescription", $"Prescription print preview initialized for {Prescription.PrescriptionNumber}.");
    }

    private async Task CancelPrescriptionAsync()
    {
        if (Prescription == null) return;

        var confirmed = _dialogService.ShowConfirmation(
            "Cancel Prescription",
            $"Are you sure you want to cancel prescription {Prescription.PrescriptionNumber}?\n\nThe prescription will remain on record marked as cancelled and cannot be uncancelled.",
            confirmText: "Cancel Prescription",
            cancelText: "Keep Prescription");

        if (!confirmed) return;

        var result = await _prescriptionService.CancelPrescriptionAsync(Prescription.Id, "Cancelled by physician from prescription view.");
        if (result.IsSuccess)
        {
            _dialogService.ShowInformation("Prescription Cancelled", $"Prescription {Prescription.PrescriptionNumber} has been marked as Cancelled.");
            Prescription = await _prescriptionService.GetPrescriptionByIdAsync(Prescription.Id);
        }
        else
        {
            _dialogService.ShowError("Cancellation Failed", result.ErrorMessage ?? "Unable to cancel prescription.");
        }
    }

    private void EditPrescription()
    {
        if (Prescription == null) return;

        var confirmed = _dialogService.ShowConfirmation(
            "Edit Prescription",
            $"This prescription ({Prescription.PrescriptionNumber}) has been issued and cannot be changed directly.\n\nEditing will create a corrected copy. The original is kept on record and marked as replaced.",
            confirmText: "Create corrected copy",
            cancelText: "Cancel");

        if (!confirmed) return;

        _navigationService.NavigateTo(NavigationDestination.NewPrescription, Prescription);
    }
}
