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
    private readonly IPrescriptionService _prescriptionService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private PrescriptionDetailDto? _prescription;

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
            }
        }
    }

    public bool CanAmendOrCancel => Prescription?.Status == PrescriptionStatus.Finalized;
    public bool IsCancelled => Prescription?.Status == PrescriptionStatus.Cancelled;
    public bool IsSuperseded => Prescription?.Status == PrescriptionStatus.Superseded;
    public bool IsFinalized => Prescription?.Status == PrescriptionStatus.Finalized;

    public ICommand BackCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AmendCommand { get; }

    public PrescriptionDetailViewModel(
        IPrescriptionService prescriptionService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _prescriptionService = prescriptionService;
        _navigationService = navigationService;
        _dialogService = dialogService;

        BackCommand = new RelayCommand(() => _navigationService.NavigateTo(NavigationDestination.Dashboard));
        PrintCommand = new RelayCommand(PrintPrescription);
        CancelCommand = new AsyncRelayCommand(CancelPrescriptionAsync);
        AmendCommand = new RelayCommand(AmendPrescription);
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        if (parameter is int prescriptionId)
        {
            Prescription = await _prescriptionService.GetPrescriptionByIdAsync(prescriptionId);
        }
        else if (parameter is PrescriptionDetailDto detail)
        {
            Prescription = detail;
        }
    }

    private void PrintPrescription()
    {
        _dialogService.ShowInformation("Print Prescription", "Prescription print preview is initialized for standard A4/A5 clinical pads.");
    }

    private async Task CancelPrescriptionAsync()
    {
        if (Prescription == null) return;

        var confirmed = _dialogService.ShowConfirmation(
            "Cancel Prescription",
            $"Are you sure you want to cancel prescription {Prescription.PrescriptionNumber}? Cancelled prescriptions become read-only and cannot be undone.");

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

    private void AmendPrescription()
    {
        if (Prescription == null) return;
        _navigationService.NavigateTo(NavigationDestination.NewPrescription, Prescription);
    }
}
