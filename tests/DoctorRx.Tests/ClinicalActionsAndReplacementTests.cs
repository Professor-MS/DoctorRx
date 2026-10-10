using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Domain.ValueObjects;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using Xunit;

namespace DoctorRx.Tests;

public class ClinicalActionsAndReplacementTests
{
    [Fact]
    public async Task SupersededPrescription_QueriesReplacementByParentId_AndExposesLink()
    {
        var stubPrescriptionService = new StubReplacementPrescriptionService();
        var navTracker = new TestNavigationTracker();
        var dialogService = new TestDialogTracker();

        var detailVm = new PrescriptionDetailViewModel(stubPrescriptionService, navTracker, dialogService);

        // Prescription 1 is Superseded
        var supersededRx = CreatePrescriptionDetail(1, "RX-20261009-0001", PrescriptionStatus.Superseded);
        // Replacement is RX-20261009-0001-A1 (Id 2) with ParentPrescriptionId 1
        stubPrescriptionService.SetReplacement(1, new PrescriptionSummaryDto(
            Id: 2,
            PrescriptionNumber: "RX-20261009-0001-A1",
            PatientId: 1,
            PatientName: "Patient One",
            PatientAge: "30y",
            PatientGender: Gender.Male,
            PrescriptionDate: DateOnly.FromDateTime(DateTime.Today),
            Status: PrescriptionStatus.Finalized,
            MedicineCount: 2,
            FollowUpDate: null,
            AmendmentNumber: 1
        ));

        await detailVm.InitializeAsync(supersededRx);

        Assert.True(detailVm.IsSuperseded);
        Assert.NotNull(detailVm.ReplacementPrescription);
        Assert.True(detailVm.HasReplacementPrescription);
        Assert.Equal("RX-20261009-0001-A1", detailVm.ReplacementPrescription.PrescriptionNumber);
        Assert.Equal("Replaced by RX-20261009-0001-A1", detailVm.StatusBadgeText);

        // Clicking OpenReplacement navigates to the replacement
        detailVm.OpenReplacementCommand.Execute(null);
        Assert.Equal(NavigationDestination.PrescriptionDetail, navTracker.LastNavigatedDestination);
        Assert.Equal(2, navTracker.LastParameter);
    }

    [Fact]
    public async Task SupersededPrescription_WhenNoReplacementExists_ShowsReplacedWithoutLink()
    {
        var stubPrescriptionService = new StubReplacementPrescriptionService();
        var navTracker = new TestNavigationTracker();
        var dialogService = new TestDialogTracker();

        var detailVm = new PrescriptionDetailViewModel(stubPrescriptionService, navTracker, dialogService);

        var supersededRx = CreatePrescriptionDetail(1, "RX-20261009-0001", PrescriptionStatus.Superseded);
        // No replacement set in service

        await detailVm.InitializeAsync(supersededRx);

        Assert.True(detailVm.IsSuperseded);
        Assert.Null(detailVm.ReplacementPrescription);
        Assert.False(detailVm.HasReplacementPrescription);
        Assert.Equal("Replaced", detailVm.StatusBadgeText);
    }

    [Fact]
    public async Task FinalizedPrescription_ShowsIssuedLockedStatus()
    {
        var stubPrescriptionService = new StubReplacementPrescriptionService();
        var navTracker = new TestNavigationTracker();
        var dialogService = new TestDialogTracker();

        var detailVm = new PrescriptionDetailViewModel(stubPrescriptionService, navTracker, dialogService);
        var finalizedRx = CreatePrescriptionDetail(1, "RX-20261009-0001", PrescriptionStatus.Finalized);

        await detailVm.InitializeAsync(finalizedRx);

        Assert.True(detailVm.IsFinalized);
        Assert.Equal("Issued (locked)", detailVm.StatusBadgeText);
    }

    [Fact]
    public async Task EditPrescription_PromptsPlainLanguageDialog_AndNavigatesWhenConfirmed()
    {
        var stubPrescriptionService = new StubReplacementPrescriptionService();
        var navTracker = new TestNavigationTracker();
        var dialogService = new TestDialogTracker { ConfirmationResult = true };

        var detailVm = new PrescriptionDetailViewModel(stubPrescriptionService, navTracker, dialogService);
        var rx = CreatePrescriptionDetail(10, "RX-20261009-0010", PrescriptionStatus.Finalized);

        await detailVm.InitializeAsync(rx);

        detailVm.EditPrescriptionCommand.Execute(null);

        Assert.True(dialogService.WasConfirmationShown);
        Assert.Equal("Edit Prescription", dialogService.LastConfirmationTitle);
        Assert.Contains("This prescription (RX-20261009-0010) has been issued and cannot be changed directly", dialogService.LastConfirmationMessage);
        Assert.Contains("Editing will create a corrected copy", dialogService.LastConfirmationMessage);
        Assert.Equal("Create corrected copy", dialogService.LastConfirmText);
        Assert.Equal("Cancel", dialogService.LastCancelText);

        Assert.Equal(NavigationDestination.NewPrescription, navTracker.LastNavigatedDestination);
    }

    [Fact]
    public async Task EditPrescription_WhenCancelled_DoesNotNavigate()
    {
        var stubPrescriptionService = new StubReplacementPrescriptionService();
        var navTracker = new TestNavigationTracker();
        var dialogService = new TestDialogTracker { ConfirmationResult = false };

        var detailVm = new PrescriptionDetailViewModel(stubPrescriptionService, navTracker, dialogService);
        var rx = CreatePrescriptionDetail(10, "RX-20261009-0010", PrescriptionStatus.Finalized);

        await detailVm.InitializeAsync(rx);

        detailVm.EditPrescriptionCommand.Execute(null);

        Assert.True(dialogService.WasConfirmationShown);
        Assert.Null(navTracker.LastNavigatedDestination);
    }

    [Fact]
    public async Task CancelPrescription_PromptsPlainLanguageDialog_WithProperButtons()
    {
        var stubPrescriptionService = new StubReplacementPrescriptionService();
        var navTracker = new TestNavigationTracker();
        var dialogService = new TestDialogTracker { ConfirmationResult = true };

        var detailVm = new PrescriptionDetailViewModel(stubPrescriptionService, navTracker, dialogService);
        var rx = CreatePrescriptionDetail(15, "RX-20261009-0015", PrescriptionStatus.Finalized);

        await detailVm.InitializeAsync(rx);

        detailVm.CancelCommand.Execute(null);

        Assert.True(dialogService.WasConfirmationShown);
        Assert.Equal("Cancel Prescription", dialogService.LastConfirmationTitle);
        Assert.Contains("remain on record marked as cancelled", dialogService.LastConfirmationMessage);
        Assert.Equal("Cancel Prescription", dialogService.LastConfirmText);
        Assert.Equal("Keep Prescription", dialogService.LastCancelText);
    }

    [Fact]
    public async Task ComposerInCorrectionMode_ShowsCorrectionBanner()
    {
        var patientService = new StubPatientService();
        var medicineService = new StubMedicineService();
        var prescriptionService = new StubReplacementPrescriptionService();
        var draftService = new StubDraftService();
        var dialogService = new TestDialogTracker();
        var navService = new TestNavigationTracker();
        var validator = new StubValidator();
        var clock = new StubClock();

        var composerVm = new NewPrescriptionViewModel(
            patientService,
            medicineService,
            prescriptionService,
            draftService,
            dialogService,
            navService,
            validator,
            clock);

        var parentDetail = CreatePrescriptionDetail(25, "RX-20261009-0025", PrescriptionStatus.Finalized);

        await composerVm.InitializeAsync(parentDetail);

        Assert.True(composerVm.IsAmending);
        Assert.Equal("RX-20261009-0025", composerVm.AmendmentParentPrescriptionNumber);
        Assert.Equal("You are correcting RX-20261009-0025. The original stays on record.", composerVm.CorrectionBannerText);
    }

    private static PrescriptionDetailDto CreatePrescriptionDetail(int id, string number, PrescriptionStatus status)
    {
        return new PrescriptionDetailDto(
            Id: id,
            PrescriptionNumber: number,
            PatientId: 1,
            PatientSnapshot: new PatientSnapshot("Test Patient", Gender.Female, "45y", null, null),
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
            Status: status,
            FinalizedAtUtc: DateTime.UtcNow,
            CancelledAtUtc: status == PrescriptionStatus.Cancelled ? DateTime.UtcNow : null,
            CancellationReason: status == PrescriptionStatus.Cancelled ? "Cancelled" : null,
            ParentPrescriptionId: null,
            AmendmentNumber: 0,
            Version: 1,
            Items: Array.Empty<PrescriptionMedicineDto>()
        );
    }

    private class StubReplacementPrescriptionService : IPrescriptionService
    {
        private readonly Dictionary<int, PrescriptionSummaryDto> _replacements = new();

        public void SetReplacement(int parentId, PrescriptionSummaryDto replacement)
        {
            _replacements[parentId] = replacement;
        }

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

        public Task<PrescriptionSummaryDto?> GetReplacementPrescriptionAsync(int parentPrescriptionId, CancellationToken cancellationToken = default)
        {
            _replacements.TryGetValue(parentPrescriptionId, out var replacement);
            return Task.FromResult(replacement);
        }
    }

    private class TestNavigationTracker : INavigationService
    {
        public ViewModelBase? CurrentViewModel { get; }
        public NavigationDestination CurrentDestination { get; } = NavigationDestination.Dashboard;
        public NavigationDestination? PreviousDestination => null;
        public NavigationDestination? LastNavigatedDestination { get; private set; }
        public object? LastParameter { get; private set; }
        public event Action<ViewModelBase>? CurrentViewModelChanged { add { } remove { } }

        public void NavigateTo(NavigationDestination destination, object? parameter = null)
        {
            LastNavigatedDestination = destination;
            LastParameter = parameter;
        }

        public void GoBack() { }
    }

    private class TestDialogTracker : IDialogService
    {
        public bool ConfirmationResult { get; set; } = true;
        public bool WasConfirmationShown { get; private set; }
        public string? LastConfirmationTitle { get; private set; }
        public string? LastConfirmationMessage { get; private set; }
        public string? LastConfirmText { get; private set; }
        public string? LastCancelText { get; private set; }

        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }

        public bool ShowConfirmation(string title, string message) => ShowConfirmation(title, message, "Yes", "No");

        public bool ShowConfirmation(string title, string message, string confirmText, string cancelText)
        {
            WasConfirmationShown = true;
            LastConfirmationTitle = title;
            LastConfirmationMessage = message;
            LastConfirmText = confirmText;
            LastCancelText = cancelText;
            return ConfirmationResult;
        }

        public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel") => true;
    }

    private class StubPatientService : IPatientService
    {
        public Task<PagedResult<PatientDto>> GetPatientsPagedAsync(int pageNumber, int pageSize = 50, bool showArchived = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<PatientDto>(Array.Empty<PatientDto>(), 0, pageNumber, pageSize));

        public Task<PagedResult<PatientDto>> GetFilteredPatientsPagedAsync(PatientFilterCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<PatientDto>(Array.Empty<PatientDto>(), 0, criteria.PageNumber, criteria.PageSize));

        public Task<IReadOnlyList<PatientDto>> SearchPatientsAsync(string query, int maxResults = 50, bool showArchived = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PatientDto>>(Array.Empty<PatientDto>());

        public Task<PatientDto?> GetPatientByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<PatientDto?>(new PatientDto(
                id, "MRN-001", "Test Patient", null, 45, Gender.Female, null, null, null, null, DateTime.UtcNow, null, false, null));

        public Task<IReadOnlyList<PatientDto>> GetRecentPatientsAsync(int count = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PatientDto>>(Array.Empty<PatientDto>());

        public Task<Result<PatientDto>> CreatePatientAsync(CreatePatientDto dto, bool allowDuplicate = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<PatientDto>.Failure("Stub"));

        public Task<Result<PatientDto>> UpdatePatientAsync(UpdatePatientDto dto, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<PatientDto>.Failure("Stub"));

        public Task<Result> ArchivePatientAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
        public Task<Result> RestorePatientAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
        public Task<Result> DeletePatientAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
    }

    private class StubMedicineService : IMedicineService
    {
        public Task<IReadOnlyList<MedicineDto>> GetMedicinesPagedAsync(int pageNumber, int pageSize = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MedicineDto>>(Array.Empty<MedicineDto>());

        public Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MedicineDto>>(Array.Empty<MedicineDto>());

        public Task<MedicineDto?> GetMedicineByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<MedicineDto?>(null);

        public Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(MedicineSearchCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MedicineDto>>(Array.Empty<MedicineDto>());

        public Task<Result<MedicineDto>> CreateMedicineAsync(CreateMedicineDto dto, bool allowDuplicate = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<MedicineDto>.Failure("Stub"));

        public Task<Result<MedicineDto>> UpdateMedicineAsync(UpdateMedicineDto dto, bool allowDuplicate = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<MedicineDto>.Failure("Stub"));

        public Task<Result> DeleteMedicineAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> PurgeMedicineAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<MedicineUsageSummaryDto> GetMedicineUsageSummaryAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MedicineUsageSummaryDto(id, string.Empty, false, 0, true));
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

    private class StubValidator : IPrescriptionComposerValidator
    {
        public ComposerValidationResult Validate(PrescriptionComposerState state) =>
            new ComposerValidationResult();
    }

    private class StubClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    }
}
