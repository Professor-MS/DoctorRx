using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Models;
using DoctorRx.Presentation.Services;

namespace DoctorRx.Presentation.ViewModels;

public class PrescriptionHistoryViewModel : ViewModelBase
{
    public override NavigationSection NavigationSection => NavigationSection.PrescriptionHistory;

    private readonly IPrescriptionService _prescriptionService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly IPrescriptionHistoryFilterSessionService _filterSession;
    private readonly IPrintPreviewLauncher _printLauncher;

    private CancellationTokenSource? _searchCts;
    private bool _isLoading;
    private int _totalCount;
    private bool _isCountCapped;
    private PrescriptionHistoryItemDto? _selectedPrescription;
    private string? _errorMessage;

    public IReadOnlyList<PrescriptionDatePreset> AvailableDatePresets { get; } =
        Enum.GetValues<PrescriptionDatePreset>();

    public IReadOnlyList<PrescriptionStatusSearchFilter> AvailableStatusFilters { get; } =
        Enum.GetValues<PrescriptionStatusSearchFilter>();

    public IReadOnlyList<PrescriptionSortOption> AvailableSortOptions { get; } =
        Enum.GetValues<PrescriptionSortOption>();

    public ObservableCollection<PrescriptionHistoryItemDto> Prescriptions { get; } = new();

    public PrescriptionHistoryItemDto? SelectedPrescription
    {
        get => _selectedPrescription;
        set
        {
            if (SetProperty(ref _selectedPrescription, value))
            {
                _filterSession.SelectedPrescriptionId = value?.Id;
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string SearchText
    {
        get => _filterSession.SearchText;
        set
        {
            if (_filterSession.SearchText != value)
            {
                _filterSession.SearchText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSearchText));
                OnPropertyChanged(nameof(HasActiveFilters));
                QueueDebouncedSearch();
            }
        }
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    public PrescriptionDatePreset DatePreset
    {
        get => _filterSession.DatePreset;
        set
        {
            if (_filterSession.DatePreset != value)
            {
                _filterSession.DatePreset = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomDateRange));
                OnPropertyChanged(nameof(HasActiveFilters));
                _ = LoadPrescriptionsAsync(resetPage: true);
            }
        }
    }

    public bool IsCustomDateRange => DatePreset == PrescriptionDatePreset.Custom;

    public DateTime? FromDate
    {
        get => _filterSession.FromDate?.ToDateTime(TimeOnly.MinValue);
        set
        {
            var dateOnly = value.HasValue ? DateOnly.FromDateTime(value.Value) : (DateOnly?)null;
            if (_filterSession.FromDate != dateOnly)
            {
                _filterSession.FromDate = dateOnly;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveFilters));
                if (IsCustomDateRange)
                {
                    _ = LoadPrescriptionsAsync(resetPage: true);
                }
            }
        }
    }

    public DateTime? ToDate
    {
        get => _filterSession.ToDate?.ToDateTime(TimeOnly.MinValue);
        set
        {
            var dateOnly = value.HasValue ? DateOnly.FromDateTime(value.Value) : (DateOnly?)null;
            if (_filterSession.ToDate != dateOnly)
            {
                _filterSession.ToDate = dateOnly;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveFilters));
                if (IsCustomDateRange)
                {
                    _ = LoadPrescriptionsAsync(resetPage: true);
                }
            }
        }
    }

    public PrescriptionStatusSearchFilter StatusFilter
    {
        get => _filterSession.StatusFilter;
        set
        {
            if (_filterSession.StatusFilter != value)
            {
                _filterSession.StatusFilter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveFilters));
                _ = LoadPrescriptionsAsync(resetPage: true);
            }
        }
    }

    public bool IncludeReplaced
    {
        get => _filterSession.IncludeReplaced;
        set
        {
            if (_filterSession.IncludeReplaced != value)
            {
                _filterSession.IncludeReplaced = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveFilters));
                _ = LoadPrescriptionsAsync(resetPage: true);
            }
        }
    }

    public PrescriptionSortOption SortOption
    {
        get => _filterSession.SortOption;
        set
        {
            if (_filterSession.SortOption != value)
            {
                _filterSession.SortOption = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveFilters));
                _ = LoadPrescriptionsAsync(resetPage: true);
            }
        }
    }

    public bool HasActiveFilters => _filterSession.HasActiveFilters;

    public int PageNumber
    {
        get => _filterSession.PageNumber;
        set
        {
            if (_filterSession.PageNumber != value)
            {
                _filterSession.PageNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
                OnPropertyChanged(nameof(PageSummaryText));
            }
        }
    }

    public int PageSize
    {
        get => _filterSession.PageSize;
        set
        {
            if (_filterSession.PageSize != value)
            {
                _filterSession.PageSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
                OnPropertyChanged(nameof(PageSummaryText));
            }
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set
        {
            if (SetProperty(ref _totalCount, value))
            {
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
                OnPropertyChanged(nameof(PageSummaryText));
            }
        }
    }

    public bool IsCountCapped
    {
        get => _isCountCapped;
        private set
        {
            if (SetProperty(ref _isCountCapped, value))
            {
                OnPropertyChanged(nameof(PageSummaryText));
            }
        }
    }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / Math.Max(1, PageSize)));

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    public double ScrollPosition
    {
        get => _filterSession.ScrollPosition;
        set => _filterSession.ScrollPosition = value;
    }

    public string PageSummaryText
    {
        get
        {
            if (TotalCount == 0) return "No prescriptions found";
            int start = ((PageNumber - 1) * PageSize) + 1;
            int end = Math.Min(PageNumber * PageSize, TotalCount);
            string totalStr = IsCountCapped ? $"{TotalCount:N0}+" : $"{TotalCount:N0}";
            return $"Showing {start:N0} - {end:N0} of {totalStr} prescriptions";
        }
    }

    public ICommand ClearSearchCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand FirstPageCommand { get; }
    public ICommand LastPageCommand { get; }
    public ICommand OpenDetailCommand { get; }
    public ICommand PrintPrescriptionCommand { get; }

    public PrescriptionHistoryViewModel(
        IPrescriptionService? prescriptionService = null,
        INavigationService? navigationService = null,
        IDialogService? dialogService = null,
        IPrescriptionHistoryFilterSessionService? filterSession = null,
        IPrintPreviewLauncher? printLauncher = null)
    {
        _prescriptionService = prescriptionService ?? null!;
        _navigationService = navigationService ?? null!;
        _dialogService = dialogService ?? new DialogService();
        _filterSession = filterSession ?? new PrescriptionHistoryFilterSessionService();
        _printLauncher = printLauncher ?? new PrintPreviewLauncher(_dialogService);

        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        ClearFiltersCommand = new RelayCommand(ClearAllFilters);
        RefreshCommand = new AsyncRelayCommand(() => LoadPrescriptionsAsync(resetPage: false));
        NextPageCommand = new AsyncRelayCommand(GoToNextPageAsync, () => HasNextPage);
        PreviousPageCommand = new AsyncRelayCommand(GoToPreviousPageAsync, () => HasPreviousPage);
        FirstPageCommand = new AsyncRelayCommand(GoToFirstPageAsync, () => HasPreviousPage);
        LastPageCommand = new AsyncRelayCommand(GoToLastPageAsync, () => HasNextPage);
        OpenDetailCommand = new RelayCommand<PrescriptionHistoryItemDto>(OpenPrescriptionDetail);
        PrintPrescriptionCommand = new AsyncRelayCommand<PrescriptionHistoryItemDto>(PrintPrescriptionAsync);
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        ErrorMessage = null;
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(DatePreset));
        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));
        OnPropertyChanged(nameof(IsCustomDateRange));
        OnPropertyChanged(nameof(StatusFilter));
        OnPropertyChanged(nameof(IncludeReplaced));
        OnPropertyChanged(nameof(SortOption));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(HasSearchText));

        await LoadPrescriptionsAsync(resetPage: false);
    }

    private void QueueDebouncedSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (!token.IsCancellationRequested)
                {
                    await LoadPrescriptionsAsync(resetPage: true);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when new keystroke arrives
            }
        }, token);
    }

    public async Task LoadPrescriptionsAsync(bool resetPage)
    {
        if (resetPage)
        {
            PageNumber = 1;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var criteria = new PrescriptionSearchCriteria(
                SearchText: SearchText,
                DatePreset: DatePreset,
                FromDate: _filterSession.FromDate,
                ToDate: _filterSession.ToDate,
                StatusFilter: StatusFilter,
                IncludeReplaced: IncludeReplaced,
                SortOption: SortOption,
                PageNumber: PageNumber,
                PageSize: PageSize
            );

            var result = await _prescriptionService.SearchPrescriptionsAsync(criteria);

            Prescriptions.Clear();
            foreach (var item in result.Items)
            {
                Prescriptions.Add(item);
            }

            TotalCount = result.TotalCount;
            IsCountCapped = result.IsCountCapped;

            if (_filterSession.SelectedPrescriptionId.HasValue)
            {
                SelectedPrescription = Prescriptions.FirstOrDefault(p => p.Id == _filterSession.SelectedPrescriptionId.Value);
            }

            OnPropertyChanged(nameof(HasPreviousPage));
            OnPropertyChanged(nameof(HasNextPage));
            OnPropertyChanged(nameof(PageSummaryText));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load prescriptions: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ClearAllFilters()
    {
        _filterSession.Reset();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(DatePreset));
        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));
        OnPropertyChanged(nameof(IsCustomDateRange));
        OnPropertyChanged(nameof(StatusFilter));
        OnPropertyChanged(nameof(IncludeReplaced));
        OnPropertyChanged(nameof(SortOption));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(HasSearchText));
        _ = LoadPrescriptionsAsync(resetPage: true);
    }

    private async Task GoToNextPageAsync()
    {
        if (HasNextPage)
        {
            PageNumber++;
            await LoadPrescriptionsAsync(resetPage: false);
        }
    }

    private async Task GoToPreviousPageAsync()
    {
        if (HasPreviousPage)
        {
            PageNumber--;
            await LoadPrescriptionsAsync(resetPage: false);
        }
    }

    private async Task GoToFirstPageAsync()
    {
        if (PageNumber != 1)
        {
            PageNumber = 1;
            await LoadPrescriptionsAsync(resetPage: false);
        }
    }

    private async Task GoToLastPageAsync()
    {
        if (PageNumber != TotalPages)
        {
            PageNumber = TotalPages;
            await LoadPrescriptionsAsync(resetPage: false);
        }
    }

    public void OpenPrescriptionDetail(PrescriptionHistoryItemDto? item)
    {
        var target = item ?? SelectedPrescription;
        if (target == null) return;

        _filterSession.SelectedPrescriptionId = target.Id;
        _navigationService.NavigateTo(NavigationDestination.PrescriptionDetail, target.Id);
    }

    private async Task PrintPrescriptionAsync(PrescriptionHistoryItemDto? item)
    {
        var target = item ?? SelectedPrescription;
        if (target == null) return;

        try
        {
            var detail = await _prescriptionService.GetPrescriptionByIdAsync(target.Id);
            if (detail == null)
            {
                _dialogService.ShowError("Print Error", $"Prescription {target.PrescriptionNumber} could not be loaded.");
                return;
            }

            _printLauncher.Launch(detail);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Print Error", $"Failed to open print preview: {ex.Message}");
        }
    }
}
