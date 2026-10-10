using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.ValueObjects;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using Xunit;

namespace DoctorRx.Tests;

public class NavigationSectionTests
{
    [Fact]
    public void ViewModels_HaveCorrectExplicitNavigationSection()
    {
        // 1. Dashboard -> Dashboard
        Assert.Equal(NavigationSection.Dashboard, new DashboardViewModel(
            new StubDashboardService(),
            new StubNavigationService(),
            new StubDraftService()).NavigationSection);

        // 2. Prescription History -> PrescriptionHistory
        Assert.Equal(NavigationSection.PrescriptionHistory, new PrescriptionHistoryViewModel().NavigationSection);

        // 3. Medicines -> Medicines
        Assert.Equal(NavigationSection.Medicines, new MedicinesViewModel().NavigationSection);

        // 4. Settings -> Settings
        Assert.Equal(NavigationSection.Settings, new SettingsViewModel().NavigationSection);

        // 5. PrescriptionDetail -> PrescriptionHistory
        Assert.Equal(NavigationSection.PrescriptionHistory, new PrescriptionDetailViewModel(
            new StubPrescriptionService(),
            new StubNavigationService(),
            new StubDialogService()).NavigationSection);
    }

    [Fact]
    public async Task PrescriptionDetail_WhenOpenedFromDashboard_ReturnsToDashboard()
    {
        var navService = new TestNavigationTracker();
        var rxDetailVm = new PrescriptionDetailViewModel(new StubPrescriptionService(), navService, new StubDialogService());

        // Simulate navigating from Dashboard to PrescriptionDetail
        navService.SimulateNavigation(NavigationDestination.Dashboard);
        navService.SimulateNavigation(NavigationDestination.PrescriptionDetail);

        await rxDetailVm.InitializeAsync(new PrescriptionDetailDto(
            Id: 1,
            PrescriptionNumber: "RX-001",
            PatientId: 1,
            PatientSnapshot: new PatientSnapshot("Ali", Gender.Male, "30y", null, null),
            DoctorId: 1,
            DoctorSnapshot: new DoctorSnapshot("Dr. Khan", "MBBS", "123", "Physician", null, "Clinic", null, null, null, null),
            PrescriptionDate: DateOnly.FromDateTime(DateTime.Today),
            ChiefComplaints: null,
            BloodPressure: null,
            PulseRate: null,
            Temperature: null,
            WeightKg: null,
            ClinicalNotes: null,
            GeneralAdvice: null,
            FollowUpDate: null,
            FollowUpText: null,
            Status: PrescriptionStatus.Finalized,
            FinalizedAtUtc: DateTime.UtcNow,
            CancelledAtUtc: null,
            CancellationReason: null,
            ParentPrescriptionId: null,
            AmendmentNumber: 0,
            Version: 1,
            Items: Array.Empty<PrescriptionMedicineDto>()));

        Assert.Equal(NavigationDestination.Dashboard, rxDetailVm.ReturnDestination);
        rxDetailVm.BackCommand.Execute(null);
        Assert.Equal(NavigationDestination.Dashboard, navService.LastNavigatedDestination);
    }

    [Fact]
    public async Task PrescriptionDetail_WhenOpenedFromPatients_ReturnsToPatients()
    {
        var navService = new TestNavigationTracker();
        var rxDetailVm = new PrescriptionDetailViewModel(new StubPrescriptionService(), navService, new StubDialogService());

        // Simulate navigating from Patients to PrescriptionDetail
        navService.SimulateNavigation(NavigationDestination.Patients);
        navService.SimulateNavigation(NavigationDestination.PrescriptionDetail);

        await rxDetailVm.InitializeAsync(1);

        Assert.Equal(NavigationDestination.Patients, rxDetailVm.ReturnDestination);
        rxDetailVm.BackCommand.Execute(null);
        Assert.Equal(NavigationDestination.Patients, navService.LastNavigatedDestination);
    }

    [Fact]
    public async Task PrescriptionDetail_AfterFinalizing_ReturnsToHistory_NeverComposer()
    {
        var navService = new TestNavigationTracker();
        var rxDetailVm = new PrescriptionDetailViewModel(new StubPrescriptionService(), navService, new StubDialogService());

        // Simulate navigating from NewPrescription (composer finalization) to PrescriptionDetail
        navService.SimulateNavigation(NavigationDestination.NewPrescription);
        navService.SimulateNavigation(NavigationDestination.PrescriptionDetail);

        await rxDetailVm.InitializeAsync(1);

        // MUST be PrescriptionHistory, NEVER NewPrescription
        Assert.NotEqual(NavigationDestination.NewPrescription, rxDetailVm.ReturnDestination);
        Assert.Equal(NavigationDestination.PrescriptionHistory, rxDetailVm.ReturnDestination);

        rxDetailVm.BackCommand.Execute(null);
        Assert.Equal(NavigationDestination.PrescriptionHistory, navService.LastNavigatedDestination);
    }

    [Fact]
    public void MainWindowViewModel_HighlightsPrescriptionHistory_WhenViewingPrescriptionDetail()
    {
        var navService = new TestNavigationTracker();
        var mainVm = new MainWindowViewModel(
            navService,
            new StubDoctorService(),
            new StubDialogService(),
            new StubDraftService(),
            new StubWindowPlacementService());

        var detailVm = new PrescriptionDetailViewModel(new StubPrescriptionService(), navService, new StubDialogService());

        // Simulate navigation to PrescriptionDetail
        navService.SimulateCurrentViewModelChanged(detailVm);

        Assert.Equal(NavigationSection.PrescriptionHistory, mainVm.CurrentSection);
        Assert.True(mainVm.IsPrescriptionHistorySection);
        Assert.False(mainVm.IsNewPrescriptionSection);
        Assert.False(mainVm.IsDashboardSection);
    }

    private class TestNavigationTracker : INavigationService
    {
        public ViewModelBase? CurrentViewModel { get; private set; }
        public NavigationDestination CurrentDestination { get; private set; } = NavigationDestination.Dashboard;
        public NavigationDestination? PreviousDestination { get; private set; }
        public NavigationDestination? LastNavigatedDestination { get; private set; }
        public event Action<ViewModelBase>? CurrentViewModelChanged;

        public void NavigateTo(NavigationDestination destination, object? parameter = null)
        {
            LastNavigatedDestination = destination;
            SimulateNavigation(destination);
        }

        public void GoBack()
        {
            if (PreviousDestination.HasValue)
            {
                NavigateTo(PreviousDestination.Value);
            }
        }

        public void SimulateNavigation(NavigationDestination destination)
        {
            if (destination != CurrentDestination)
            {
                PreviousDestination = CurrentDestination;
                CurrentDestination = destination;
            }
        }

        public void SimulateCurrentViewModelChanged(ViewModelBase vm)
        {
            CurrentViewModel = vm;
            CurrentViewModelChanged?.Invoke(vm);
        }
    }

    private class StubDashboardService : IDashboardService
    {
        public Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DashboardStatsDto(0, 0, 0, 0, Array.Empty<PrescriptionSummaryDto>(), Array.Empty<PatientDto>()));
    }

    private class StubPrescriptionService : IPrescriptionService
    {
        public Task<PrescriptionDetailDto?> GetPrescriptionByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<PrescriptionDetailDto?>(null);

        public Task<IReadOnlyList<PrescriptionSummaryDto>> GetRecentPrescriptionsAsync(int count = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PrescriptionSummaryDto>>(Array.Empty<PrescriptionSummaryDto>());

        public Task<IReadOnlyList<PrescriptionSummaryDto>> GetPrescriptionsByPatientIdAsync(int patientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PrescriptionSummaryDto>>(Array.Empty<PrescriptionSummaryDto>());

        public Task<Result<PrescriptionDetailDto>> FinalizePrescriptionAsync(CreatePrescriptionDto dto, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<PrescriptionDetailDto>.Failure("Stub"));

        public Task<Result<PrescriptionDetailDto>> AmendPrescriptionAsync(int originalId, CreatePrescriptionDto newContent, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<PrescriptionDetailDto>.Failure("Stub"));

        public Task<Result> CancelPrescriptionAsync(int id, string reason, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }

    private class StubDoctorService : IDoctorService
    {
        public Task<DoctorDto?> GetActiveDoctorAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DoctorDto?>(null);

        public Task<Result<DoctorDto>> CreateDoctorAsync(CreateDoctorDto dto, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<DoctorDto>.Failure("Stub"));

        public Task<Result<DoctorDto>> UpdateDoctorAsync(UpdateDoctorDto dto, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<DoctorDto>.Failure("Stub"));

        public Task<Result<DoctorDto>> SwitchActiveDoctorAsync(int doctorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<DoctorDto>.Failure("Stub"));
    }

    private class StubDialogService : IDialogService
    {
        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
        public bool ShowConfirmation(string title, string message) => true;
        public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel") => true;
    }

    private class StubDraftService : IDraftService
    {
        public Task<int> GetCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<DraftSummaryDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DraftSummaryDto>>(Array.Empty<DraftSummaryDto>());
        public Task<Result<PrescriptionComposerState>> GetAsync(Guid draftKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<PrescriptionComposerState>.Failure("Stub"));
        public Task<Result<Guid>> SaveAsync(PrescriptionComposerState state, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        public Task<Result> DiscardAsync(Guid draftKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
        public void MarkFinalized(Guid draftKey) { }
        public bool IsFinalized(Guid draftKey) => false;
    }

    private class StubNavigationService : INavigationService
    {
        public ViewModelBase? CurrentViewModel => null;
        public NavigationDestination CurrentDestination => NavigationDestination.Dashboard;
        public NavigationDestination? PreviousDestination => null;
        public event Action<ViewModelBase>? CurrentViewModelChanged { add { } remove { } }
        public void NavigateTo(NavigationDestination destination, object? parameter = null) { }
        public void GoBack() { }
    }

    private class StubWindowPlacementService : IWindowPlacementService
    {
        public WindowPlacementSettings? LoadPlacement() => null;
        public void SavePlacement(WindowPlacementSettings settings) { }
        public void ApplyPlacement(Window window) { }
        public void PersistPlacement(Window window, bool? isSidebarCollapsedOverride = null) { }
        public bool IsOnScreen(double left, double top, double width, double height) => true;
    }
}
