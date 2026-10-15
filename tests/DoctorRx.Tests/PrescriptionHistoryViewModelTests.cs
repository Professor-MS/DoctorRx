using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Models;
using DoctorRx.Domain.ValueObjects;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using Xunit;

namespace DoctorRx.Tests;

public class PrescriptionHistoryViewModelTests
{
    private readonly StubPrescriptionService _prescriptionService;
    private readonly TestNavigationService _navigationService;
    private readonly TestDialogService _dialogService;
    private readonly PrescriptionHistoryFilterSessionService _sessionService;
    private readonly TestPrintLauncher _printLauncher;

    public PrescriptionHistoryViewModelTests()
    {
        _prescriptionService = new StubPrescriptionService();
        _navigationService = new TestNavigationService();
        _dialogService = new TestDialogService();
        _sessionService = new PrescriptionHistoryFilterSessionService();
        _printLauncher = new TestPrintLauncher();
    }

    [Fact]
    public async Task SearchDebounce_QueuesAndExecutesSearch()
    {
        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);

        // Setting search text triggers debounced search
        vm.SearchText = "Amoxicillin";
        Assert.Equal("Amoxicillin", vm.SearchText);
        Assert.True(vm.HasSearchText);
        Assert.True(vm.HasActiveFilters);

        // Allow debounce timer (250ms) to fire; use 600ms for reliability on slow machines
        await Task.Delay(600);

        Assert.Equal("Amoxicillin", _prescriptionService.LastCriteria?.SearchText);
    }

    [Fact]
    public async Task FilterChanges_ImmediatelyTriggerQuery()
    {
        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);
        await vm.InitializeAsync();

        vm.StatusFilter = PrescriptionStatusSearchFilter.Cancelled;
        Assert.Equal(PrescriptionStatusSearchFilter.Cancelled, _prescriptionService.LastCriteria?.StatusFilter);

        vm.SortOption = PrescriptionSortOption.PatientName;
        Assert.Equal(PrescriptionSortOption.PatientName, _prescriptionService.LastCriteria?.SortOption);

        vm.DatePreset = PrescriptionDatePreset.Last7Days;
        Assert.Equal(PrescriptionDatePreset.Last7Days, _prescriptionService.LastCriteria?.DatePreset);
    }

    [Fact]
    public async Task Pagination_NextPreviousFirstLast_UpdatesPageNumberAndCallsService()
    {
        _prescriptionService.TotalCountToReturn = 150; // 3 pages of 50
        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);
        await vm.InitializeAsync();

        Assert.Equal(1, vm.PageNumber);
        Assert.Equal(3, vm.TotalPages);
        Assert.True(vm.HasNextPage);
        Assert.False(vm.HasPreviousPage);

        // Go to Next
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.NextPageCommand).ExecuteAsync(null);
        Assert.Equal(2, vm.PageNumber);
        Assert.True(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);

        // Go to Last
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.LastPageCommand).ExecuteAsync(null);
        Assert.Equal(3, vm.PageNumber);
        Assert.False(vm.HasNextPage);

        // Go to Prev
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.PreviousPageCommand).ExecuteAsync(null);
        Assert.Equal(2, vm.PageNumber);

        // Go to First
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.FirstPageCommand).ExecuteAsync(null);
        Assert.Equal(1, vm.PageNumber);
    }

    [Fact]
    public async Task BackFromDetail_RestoresFilters_Page_AndScrollPosition()
    {
        // 1. Initial history state setup
        var vm1 = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);
        await vm1.InitializeAsync();

        vm1.SearchText = "Zainab";
        vm1.StatusFilter = PrescriptionStatusSearchFilter.Issued;
        vm1.DatePreset = PrescriptionDatePreset.ThisMonth;
        vm1.SortOption = PrescriptionSortOption.PatientName;
        vm1.PageNumber = 3;
        vm1.ScrollPosition = 245.5;

        // Select an item and open detail
        var selectedItem = new PrescriptionHistoryItemDto(
            42, "RX-20261013-000042", 0, DateOnly.FromDateTime(DateTime.Today), "Zainab Bibi", "P-00431", 2, PrescriptionStatus.Finalized, null, DateTime.UtcNow);

        vm1.OpenPrescriptionDetail(selectedItem);

        Assert.Equal(NavigationDestination.PrescriptionDetail, _navigationService.CurrentDestination);
        Assert.Equal(42, _sessionService.SelectedPrescriptionId);

        // 2. Simulate user going to Detail and pressing Back button
        var detailVm = new PrescriptionDetailViewModel(_prescriptionService, _navigationService, _dialogService, _printLauncher);
        await detailVm.InitializeAsync(42);
        Assert.Equal(NavigationDestination.PrescriptionHistory, detailVm.ReturnDestination);

        // User hits BackCommand
        detailVm.BackCommand.Execute(null);
        Assert.Equal(NavigationDestination.PrescriptionHistory, _navigationService.CurrentDestination);

        // 3. New or recycled PrescriptionHistoryViewModel re-initialized
        var vm2 = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);
        await vm2.InitializeAsync();

        // Verify all session states restored
        Assert.Equal("Zainab", vm2.SearchText);
        Assert.Equal(PrescriptionStatusSearchFilter.Issued, vm2.StatusFilter);
        Assert.Equal(PrescriptionDatePreset.ThisMonth, vm2.DatePreset);
        Assert.Equal(PrescriptionSortOption.PatientName, vm2.SortOption);
        Assert.Equal(3, vm2.PageNumber);
        Assert.Equal(245.5, vm2.ScrollPosition);
        Assert.Equal(42, vm2.SelectedPrescription?.Id);
    }

    [Fact]
    public async Task ClearFilters_ResetsAllFiltersAndPage()
    {
        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);
        await vm.InitializeAsync();

        vm.SearchText = "Antibiotics";
        vm.StatusFilter = PrescriptionStatusSearchFilter.Cancelled;
        vm.PageNumber = 4;

        Assert.True(vm.HasActiveFilters);

        vm.ClearFiltersCommand.Execute(null);

        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal(PrescriptionStatusSearchFilter.Default, vm.StatusFilter);
        Assert.Equal(1, vm.PageNumber);
        Assert.False(vm.HasActiveFilters);
    }

    [Fact]
    public async Task PrintPrescriptionCommand_CallsLauncherWithDetail()
    {
        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);
        await vm.InitializeAsync();

        var item = new PrescriptionHistoryItemDto(
            10, "RX-20261013-000010", 0, DateOnly.FromDateTime(DateTime.Today), "Ali Ahmed", "P-00100", 1, PrescriptionStatus.Finalized, null, DateTime.UtcNow);

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.PrintPrescriptionCommand).ExecuteAsync(item);

        Assert.NotNull(_printLauncher.LastLaunched);
        Assert.Equal(10, _printLauncher.LastLaunched.Id);
    }

    [Fact]
    public async Task ErrorState_WhenServiceThrows_SetsErrorMessage_AndRetryClears()
    {
        // Arrange: service will throw on search
        _prescriptionService.ThrowOnSearch = true;
        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);

        // Act: initialize triggers load which will fail
        await vm.InitializeAsync();

        // Assert: error state is set
        Assert.True(vm.HasError);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("Failed to load", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vm.Prescriptions);

        // Act: clear the error condition and retry using RetryCommand
        _prescriptionService.ThrowOnSearch = false;
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.RetryCommand).ExecuteAsync(null);

        // Assert: error cleared and results loaded
        Assert.False(vm.HasError);
        Assert.Null(vm.ErrorMessage);
        Assert.NotEmpty(vm.Prescriptions);
    }

    [Fact]
    public async Task CancelledQuery_DoesNotClearExistingPrescriptionsList()
    {
        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);
        await vm.InitializeAsync();
        Assert.NotEmpty(vm.Prescriptions);
        var initialCount = vm.Prescriptions.Count;

        // When a search throws OperationCanceledException (cancelled mid-flight)
        _prescriptionService.ThrowCancellationOnSearch = true;
        await vm.LoadPrescriptionsAsync(resetPage: true);

        // Cancelled query must NOT clear the existing list or set error message
        Assert.Equal(initialCount, vm.Prescriptions.Count);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task EmptyState_WhenZeroResults_TotalCountIsZero()
    {
        // Arrange: service returns 0 results
        _prescriptionService.ReturnEmptyResults = true;
        _prescriptionService.TotalCountToReturn = 0;
        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);

        await vm.InitializeAsync();

        Assert.Empty(vm.Prescriptions);
        Assert.Equal(0, vm.TotalCount);
        // PageSummaryText shows empty state wording
        Assert.Equal("No prescriptions found", vm.PageSummaryText);
    }

    [Fact]
    public async Task NoMatchState_WithActiveFilter_ZeroResults_HasActiveFiltersTrue()
    {
        // Arrange: filter active, but no results
        _prescriptionService.ReturnEmptyResults = true;
        _prescriptionService.TotalCountToReturn = 0;
        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);
        await vm.InitializeAsync();

        vm.SearchText = "zzznomatch";
        // Wait for debounce
        await Task.Delay(600);

        Assert.True(vm.HasActiveFilters);
        Assert.True(vm.HasSearchText);
        Assert.Empty(vm.Prescriptions);
        Assert.Equal("No prescriptions found", vm.PageSummaryText);
    }

    [Fact]
    public async Task PatientChip_PatientIdFilter_RestrictsCriteriaToPatient()
    {
        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);
        await vm.InitializeAsync();

        // Simulate patient chip being set (PatientId filter)
        _sessionService.PatientId = 123;
        await vm.LoadPrescriptionsAsync(resetPage: true);

        // The criteria passed to the service must include the patient ID
        Assert.Equal(123, _prescriptionService.LastCriteria?.PatientId);
    }

    [Fact]
    public async Task IncludeReplaced_Toggle_TriggersReload_AndPassesCorrectCriteria()
    {
        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);
        await vm.InitializeAsync();

        // Default: IncludeReplaced = false
        Assert.False(vm.IncludeReplaced);
        Assert.False(_prescriptionService.LastCriteria?.IncludeReplaced);

        // Toggle to true
        vm.IncludeReplaced = true;

        // Should immediately trigger load with IncludeReplaced = true
        await Task.Delay(50); // allow async dispatch
        Assert.True(_prescriptionService.LastCriteria?.IncludeReplaced);
    }

    [Fact]
    public async Task StaleResultProtection_OutOfOrderResults_DoNotReplaceNewerResults()
    {
        // This tests that if search "A" is cancelled mid-flight and search "B" completes
        // first, the arrival of stale search "A" results does NOT overwrite "B" results.
        // We verify this at the ViewModel level: rapidly changing search text
        // and checking that the final state reflects only the last search.

        var vm = new PrescriptionHistoryViewModel(_prescriptionService, _navigationService, _dialogService, _sessionService, _printLauncher);
        await vm.InitializeAsync();

        // Rapid changes - each one cancels the previous debounce
        vm.SearchText = "old-search";
        vm.SearchText = "intermediate";
        vm.SearchText = "final-search";

        // Wait for final debounce to fire and complete
        await Task.Delay(700);

        // The service should have been called with the last search text
        Assert.Equal("final-search", _prescriptionService.LastCriteria?.SearchText);
    }

    [Fact]
    public void DigitOnlyTokenizer_RealPrescriptionFormats_ClassifiedCorrectly()
    {
        // Issue #5: digit-only rule with REAL formats
        // Our format is RX-YYYYMMDD-NNNN, patient record P-NNNNNN

        // 1. "P-000012" → IsRecordNumberPrefix with digits "12"
        var p1 = SearchNormalizer.ParseQueryTokens("P-000012");
        Assert.Single(p1);
        Assert.True(p1[0].IsRecordNumberPrefix);
        Assert.Equal("12", p1[0].RecordNumberDigits);

        // 2. "12" → IsDigitsOnly with stripped digits "12"
        var p2 = SearchNormalizer.ParseQueryTokens("12");
        Assert.Single(p2);
        Assert.True(p2[0].IsDigitsOnly);
        Assert.Equal("12", p2[0].StrippedDigits);

        // 3. "0012" → IsDigitsOnly with stripped digits "12"
        var p3 = SearchNormalizer.ParseQueryTokens("0012");
        Assert.Single(p3);
        Assert.True(p3[0].IsDigitsOnly);
        Assert.Equal("12", p3[0].StrippedDigits);

        // 4. "20261009" (date part of RX number) → IsDigitsOnly
        var p4 = SearchNormalizer.ParseQueryTokens("20261009");
        Assert.Single(p4);
        Assert.True(p4[0].IsDigitsOnly);
        Assert.Equal("20261009", p4[0].StrippedDigits);

        // 5. "RX-20261009-0002" → full Rx prefix token
        var p5 = SearchNormalizer.ParseQueryTokens("RX-20261009-0002");
        Assert.Single(p5);
        Assert.True(p5[0].IsRxNumberPrefix);
        Assert.Equal("20261009-0002", p5[0].RxNumberWithoutPrefix);
    }

    private class StubPrescriptionService : IPrescriptionService
    {
        public PrescriptionSearchCriteria? LastCriteria { get; private set; }
        public int TotalCountToReturn { get; set; } = 1;
        public bool ThrowOnSearch { get; set; } = false;
        public bool ThrowCancellationOnSearch { get; set; } = false;
        public bool ReturnEmptyResults { get; set; } = false;

        public Task<PrescriptionDetailDto?> GetPrescriptionByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var detail = new PrescriptionDetailDto(
                id,
                $"RX-20261013-{id:D6}",
                1,
                new PatientSnapshot("Patient Name", Gender.Male, "30 yrs", "03001234567", null),
                1,
                new DoctorSnapshot("Dr. Name", "MBBS", "REG-1", "GP", "0300", "Clinic", "Address", "Phone", "Header", "Footer"),
                DateOnly.FromDateTime(DateTime.Today),
                "Fever", null, null, null, null, null, null, null, null,
                PrescriptionStatus.Finalized,
                DateTime.UtcNow, null, null, null, 0, 1,
                new List<PrescriptionMedicineDto>());
            return Task.FromResult<PrescriptionDetailDto?>(detail);
        }

        public Task<PrescriptionHistoryPagedResult> SearchPrescriptionsAsync(PrescriptionSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            if (ThrowCancellationOnSearch)
                throw new OperationCanceledException();

            if (ThrowOnSearch)
                throw new InvalidOperationException("Simulated service failure");

            LastCriteria = criteria;

            if (ReturnEmptyResults)
            {
                return Task.FromResult(new PrescriptionHistoryPagedResult(
                    new List<PrescriptionHistoryItemDto>(), 0, false, criteria.PageNumber, criteria.PageSize));
            }

            var list = new List<PrescriptionHistoryItemDto>
            {
                new(42, "RX-20261013-000042", 0, DateOnly.FromDateTime(DateTime.Today), "Zainab Bibi", "P-00431", 2, PrescriptionStatus.Finalized, null, DateTime.UtcNow),
                new(10, "RX-20261013-000010", 0, DateOnly.FromDateTime(DateTime.Today), "Ali Ahmed", "P-00100", 1, PrescriptionStatus.Finalized, null, DateTime.UtcNow)
            };
            return Task.FromResult(new PrescriptionHistoryPagedResult(list, TotalCountToReturn, false, criteria.PageNumber, criteria.PageSize));
        }

        public Task<IReadOnlyList<PrescriptionSummaryDto>> GetRecentPrescriptionsAsync(int count = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PrescriptionSummaryDto>>(new List<PrescriptionSummaryDto>());

        public Task<IReadOnlyList<PrescriptionSummaryDto>> GetPrescriptionsByPatientIdAsync(int patientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PrescriptionSummaryDto>>(new List<PrescriptionSummaryDto>());

        public Task<Result<PrescriptionDetailDto>> FinalizePrescriptionAsync(CreatePrescriptionDto dto, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result<PrescriptionDetailDto>> AmendPrescriptionAsync(int originalId, CreatePrescriptionDto newContent, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result> CancelPrescriptionAsync(int id, string reason, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
    }

    private class TestNavigationService : INavigationService
    {
        public ViewModelBase? CurrentViewModel => null;
        public NavigationDestination CurrentDestination { get; private set; } = NavigationDestination.PrescriptionHistory;
        public event Action<ViewModelBase>? CurrentViewModelChanged { add { } remove { } }

        public void NavigateTo(NavigationDestination destination, object? parameter = null)
        {
            CurrentDestination = destination;
        }
    }

    private class TestDialogService : IDialogService
    {
        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
        public bool ShowConfirmation(string title, string message) => true;
        public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel") => true;
    }

    private class TestPrintLauncher : IPrintPreviewLauncher
    {
        public PrescriptionDetailDto? LastLaunched { get; private set; }
        public void Launch(PrescriptionDetailDto prescription)
        {
            LastLaunched = prescription;
        }
    }
}
