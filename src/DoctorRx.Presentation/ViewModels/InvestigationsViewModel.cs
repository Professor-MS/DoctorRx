using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Presentation.ViewModels;

public class SelectedInvestigationItemState : ObservableObject
{
    private int? _catalogItemId;
    private string _investigationName = string.Empty;
    private string _category = "Laboratory";
    private string? _subcategory;
    private string? _bodyPartOrSite;
    private string? _viewOrProtocol;
    private string? _specificInstructions;
    private int _displayOrder = 1;

    public int? CatalogItemId
    {
        get => _catalogItemId;
        set => SetProperty(ref _catalogItemId, value);
    }

    public string InvestigationName
    {
        get => _investigationName;
        set => SetProperty(ref _investigationName, value);
    }

    public string Category
    {
        get => _category;
        set
        {
            if (SetProperty(ref _category, value))
            {
                OnPropertyChanged(nameof(IsImaging));
                OnPropertyChanged(nameof(IsLaboratory));
                OnPropertyChanged(nameof(IsOtherDiagnostic));
                OnPropertyChanged(nameof(DisplayCategoryBadge));
            }
        }
    }

    public string? Subcategory
    {
        get => _subcategory;
        set
        {
            if (SetProperty(ref _subcategory, value))
            {
                OnPropertyChanged(nameof(DisplayCategoryBadge));
            }
        }
    }

    public string? BodyPartOrSite
    {
        get => _bodyPartOrSite;
        set
        {
            if (SetProperty(ref _bodyPartOrSite, value))
            {
                OnPropertyChanged(nameof(FormattedDetails));
                OnPropertyChanged(nameof(HasFormattedDetails));
            }
        }
    }

    public string? ViewOrProtocol
    {
        get => _viewOrProtocol;
        set
        {
            if (SetProperty(ref _viewOrProtocol, value))
            {
                OnPropertyChanged(nameof(FormattedDetails));
                OnPropertyChanged(nameof(HasFormattedDetails));
            }
        }
    }

    public string? SpecificInstructions
    {
        get => _specificInstructions;
        set
        {
            if (SetProperty(ref _specificInstructions, value))
            {
                OnPropertyChanged(nameof(HasSpecificInstructions));
            }
        }
    }

    public int DisplayOrder
    {
        get => _displayOrder;
        set
        {
            if (SetProperty(ref _displayOrder, value))
            {
                OnPropertyChanged(nameof(ItemNumber));
            }
        }
    }

    public int ItemNumber => DisplayOrder;

    public string DisplayCategoryBadge => string.IsNullOrWhiteSpace(Subcategory) ? Category : $"{Category} • {Subcategory}";

    public bool IsImaging => Category.Contains("Radiology", StringComparison.OrdinalIgnoreCase) ||
                             Category.Contains("Imaging", StringComparison.OrdinalIgnoreCase);

    public bool IsLaboratory => Category.Contains("Lab", StringComparison.OrdinalIgnoreCase);

    public bool IsOtherDiagnostic => !IsImaging && !IsLaboratory;

    public string FormattedDetails
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(BodyPartOrSite)) parts.Add($"Site/Specimen: {BodyPartOrSite.Trim()}");
            if (!string.IsNullOrWhiteSpace(ViewOrProtocol)) parts.Add($"Protocol: {ViewOrProtocol.Trim()}");
            return parts.Count > 0 ? string.Join(" • ", parts) : string.Empty;
        }
    }

    public bool HasFormattedDetails => !string.IsNullOrWhiteSpace(FormattedDetails);
    public bool HasSpecificInstructions => !string.IsNullOrWhiteSpace(SpecificInstructions);
}

public class InvestigationsViewModel : ViewModelBase
{
    public override NavigationSection NavigationSection => NavigationSection.Investigations;

    private readonly IInvestigationService _investigationService;
    private readonly IPatientService _patientService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly IInvestigationPrintLauncher? _printLauncher;
    private readonly ILogger<InvestigationsViewModel>? _logger;

    private CancellationTokenSource? _patientSearchCts;

    // View Modes: "NewOrder" or "History"
    private bool _isHistoryView;

    // Patient Selection
    private string _patientSearchQuery = string.Empty;
    private bool _isPatientSearching;
    private PatientDto? _selectedPatient;

    // Request Order Form
    private DateOnly _requestDate = DateOnly.FromDateTime(DateTime.Today);
    private string? _clinicalNotes;
    private string? _generalInstructions;
    private string? _errorMessage;
    private string? _successMessage;

    // Direct Test Input Editor State
    private string _editorInvestigationName = string.Empty;
    private string _editorCategory = "Laboratory";
    private string? _editorBodyPartOrSite;
    private string? _editorViewOrProtocol;
    private string? _editorSpecificInstructions;
    private bool _isEditingItem;
    private SelectedInvestigationItemState? _editingItem;
    private string? _editorErrorMessage;

    // History & Search
    private string _historySearchQuery = string.Empty;
    private DateOnly? _historyFromDate;
    private DateOnly? _historyToDate;
    private InvestigationRequestSummaryDto? _selectedHistoryRequest;
    private InvestigationRequestDetailDto? _selectedHistoryDetail;
    private bool _isLoadingHistory;

    #region Collections

    public ObservableCollection<string> AvailableCategories { get; } = new()
    {
        "Laboratory",
        "Radiology & Imaging",
        "Cardiology & Diagnostics",
        "Other Diagnostics"
    };

    public ObservableCollection<PatientDto> PatientSearchResults { get; } = new();
    public ObservableCollection<SelectedInvestigationItemState> SelectedInvestigations { get; } = new();
    public ObservableCollection<InvestigationRequestSummaryDto> HistoryRequests { get; } = new();

    #endregion

    #region Properties

    public bool IsHistoryView
    {
        get => _isHistoryView;
        set
        {
            if (SetProperty(ref _isHistoryView, value))
            {
                OnPropertyChanged(nameof(IsNewOrderView));
                OnPropertyChanged(nameof(HistoryTabButtonText));
                if (_isHistoryView)
                {
                    _ = LoadHistoryAsync();
                }
            }
        }
    }

    public bool IsNewOrderView => !IsHistoryView;
    public string HistoryTabButtonText => IsHistoryView ? "Create Order" : "Order History";

    // Patient Properties
    public string PatientSearchQuery
    {
        get => _patientSearchQuery;
        set
        {
            if (SetProperty(ref _patientSearchQuery, value))
            {
                TriggerDebouncedPatientSearch();
            }
        }
    }

    public bool IsPatientSearching
    {
        get => _isPatientSearching;
        set => SetProperty(ref _isPatientSearching, value);
    }

    public PatientDto? SelectedPatient
    {
        get => _selectedPatient;
        set
        {
            if (SetProperty(ref _selectedPatient, value))
            {
                OnPropertyChanged(nameof(HasSelectedPatient));
                OnPropertyChanged(nameof(SelectedPatientSummary));
                OnPropertyChanged(nameof(PatientDisplayName));
                OnPropertyChanged(nameof(PatientMrn));
                OnPropertyChanged(nameof(PatientAgeGender));
                OnPropertyChanged(nameof(PatientPhone));
            }
        }
    }

    public bool HasSelectedPatient => SelectedPatient != null;

    public string SelectedPatientSummary => SelectedPatient != null
        ? $"{SelectedPatient.Name} ({SelectedPatient.RecordNumber}) - {SelectedPatient.DisplayAgeGender}"
        : "No patient selected";

    public string PatientDisplayName => SelectedPatient?.Name ?? string.Empty;
    public string PatientMrn => SelectedPatient != null ? $"MRN: {SelectedPatient.RecordNumber}" : string.Empty;
    public string PatientAgeGender => SelectedPatient?.DisplayAgeGender ?? string.Empty;
    public string PatientPhone => SelectedPatient != null ? (string.IsNullOrWhiteSpace(SelectedPatient.Phone) ? "No contact" : SelectedPatient.Phone) : string.Empty;

    public bool HasPatientSearchResults => PatientSearchResults.Count > 0;
    public string SearchResultCountText => $"{PatientSearchResults.Count} patient{(PatientSearchResults.Count == 1 ? "" : "s")} found";

    // Direct Test Input Editor Properties
    public string EditorInvestigationName
    {
        get => _editorInvestigationName;
        set
        {
            if (SetProperty(ref _editorInvestigationName, value))
            {
                if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(EditorErrorMessage))
                {
                    EditorErrorMessage = null;
                }
            }
        }
    }

    public string EditorCategory
    {
        get => _editorCategory;
        set => SetProperty(ref _editorCategory, value);
    }

    public string? EditorBodyPartOrSite
    {
        get => _editorBodyPartOrSite;
        set => SetProperty(ref _editorBodyPartOrSite, value);
    }

    public string? EditorViewOrProtocol
    {
        get => _editorViewOrProtocol;
        set => SetProperty(ref _editorViewOrProtocol, value);
    }

    public string? EditorSpecificInstructions
    {
        get => _editorSpecificInstructions;
        set => SetProperty(ref _editorSpecificInstructions, value);
    }

    public bool IsEditingItem
    {
        get => _isEditingItem;
        set
        {
            if (SetProperty(ref _isEditingItem, value))
            {
                OnPropertyChanged(nameof(EditorHeaderTitle));
                OnPropertyChanged(nameof(AddOrUpdateInvestigationButtonText));
            }
        }
    }

    public string EditorHeaderTitle => IsEditingItem
        ? "Edit Prescribed Investigation / Test"
        : "Prescribe Diagnostic Investigation / Test";

    public string AddOrUpdateInvestigationButtonText => IsEditingItem
        ? "Update Investigation"
        : "+ Add Investigation to Order";

    public string? EditorErrorMessage
    {
        get => _editorErrorMessage;
        set
        {
            if (SetProperty(ref _editorErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasEditorError));
            }
        }
    }

    public bool HasEditorError => !string.IsNullOrWhiteSpace(EditorErrorMessage);

    // Order Properties
    public DateOnly RequestDate
    {
        get => _requestDate;
        set => SetProperty(ref _requestDate, value);
    }

    public string? ClinicalNotes
    {
        get => _clinicalNotes;
        set => SetProperty(ref _clinicalNotes, value);
    }

    public string? GeneralInstructions
    {
        get => _generalInstructions;
        set => SetProperty(ref _generalInstructions, value);
    }

    public string? PatientInstructions
    {
        get => GeneralInstructions;
        set => GeneralInstructions = value;
    }

    // Feedback & Notifications
    public string? SuccessMessage
    {
        get => _successMessage;
        set
        {
            if (SetProperty(ref _successMessage, value))
            {
                OnPropertyChanged(nameof(HasSuccess));
            }
        }
    }

    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
                OnPropertyChanged(nameof(HasValidationError));
                OnPropertyChanged(nameof(ValidationErrorMessage));
            }
        }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string? ValidationErrorMessage
    {
        get => ErrorMessage;
        set => ErrorMessage = value;
    }

    public bool HasValidationError => HasErrorMessage;

    public int SelectedInvestigationCount => SelectedInvestigations.Count;
    public bool HasSelectedInvestigations => SelectedInvestigations.Count > 0;
    public string OrderCountSummary => $"{SelectedInvestigations.Count} test{(SelectedInvestigations.Count == 1 ? "" : "s")} in current order";

    public int ItemsCount => SelectedInvestigationCount;
    public bool HasOrderItems => HasSelectedInvestigations;
    public ObservableCollection<SelectedInvestigationItemState> OrderItems => SelectedInvestigations;

    // History Properties
    public string HistorySearchQuery
    {
        get => _historySearchQuery;
        set
        {
            if (SetProperty(ref _historySearchQuery, value))
            {
                _ = SearchHistoryAsync();
            }
        }
    }

    public DateOnly? HistoryFromDate
    {
        get => _historyFromDate;
        set => SetProperty(ref _historyFromDate, value);
    }

    public DateOnly? HistoryToDate
    {
        get => _historyToDate;
        set => SetProperty(ref _historyToDate, value);
    }

    public InvestigationRequestSummaryDto? SelectedHistoryRequest
    {
        get => _selectedHistoryRequest;
        set
        {
            if (SetProperty(ref _selectedHistoryRequest, value) && value != null)
            {
                _ = LoadHistoryDetailAsync(value.Id);
            }
        }
    }

    public InvestigationRequestDetailDto? SelectedHistoryDetail
    {
        get => _selectedHistoryDetail;
        set
        {
            if (SetProperty(ref _selectedHistoryDetail, value))
            {
                OnPropertyChanged(nameof(HasSelectedHistoryDetail));
                OnPropertyChanged(nameof(HasSelectedHistoryRequest));
                OnPropertyChanged(nameof(SelectedRequestDetail));
            }
        }
    }

    public bool HasSelectedHistoryDetail => SelectedHistoryDetail != null;
    public bool HasSelectedHistoryRequest => SelectedHistoryDetail != null;
    public InvestigationRequestDetailDto? SelectedRequestDetail => SelectedHistoryDetail;

    public bool IsLoadingHistory
    {
        get => _isLoadingHistory;
        set => SetProperty(ref _isLoadingHistory, value);
    }

    #endregion

    #region Commands

    public ICommand SwitchToNewOrderCommand { get; }
    public ICommand SwitchToHistoryCommand { get; }
    public ICommand ToggleViewModeCommand { get; }
    public ICommand DismissBannerCommand { get; }

    public ICommand SelectPatientCommand { get; }
    public ICommand ClearPatientCommand { get; }
    public ICommand ChangePatientCommand { get; }

    public ICommand AddOrUpdateInvestigationCommand { get; }
    public ICommand EditInvestigationRowCommand { get; }
    public ICommand CancelEditInvestigationCommand { get; }
    public ICommand DeleteInvestigationRowCommand { get; }
    public ICommand RemoveInvestigationCommand { get; }
    public ICommand RemoveOrderItemCommand => RemoveInvestigationCommand;

    public ICommand SelectCategoryChipCommand { get; }
    public ICommand SelectSiteChipCommand { get; }
    public ICommand SelectProtocolChipCommand { get; }

    public ICommand ClearOrderCommand { get; }
    public ICommand SaveOrderCommand { get; }
    public ICommand SaveRequestCommand => SaveOrderCommand;
    public ICommand PrintPreviewOrderCommand { get; }
    public ICommand PrintPreviewCommand => PrintPreviewOrderCommand;
    public ICommand PrintOrderDirectCommand { get; }
    public ICommand SaveAndPrintCommand => PrintOrderDirectCommand;

    public ICommand RefreshHistoryCommand { get; }
    public ICommand ApplyHistoryFilterCommand => RefreshHistoryCommand;
    public ICommand ClearHistoryFilterCommand { get; }
    public ICommand SelectHistoryRequestCommand { get; }
    public ICommand PrintHistoryDetailCommand { get; }
    public ICommand PreviewHistoryDetailCommand { get; }
    public ICommand ReprintRequestCommand => PrintHistoryDetailCommand;

    #endregion

    public InvestigationsViewModel(
        IInvestigationService investigationService,
        IPatientService patientService,
        IDialogService dialogService,
        INavigationService navigationService,
        IInvestigationPrintLauncher? printLauncher = null,
        ILogger<InvestigationsViewModel>? logger = null)
    {
        _investigationService = investigationService;
        _patientService = patientService;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _printLauncher = printLauncher;
        _logger = logger;

        SwitchToNewOrderCommand = new RelayCommand(() => IsHistoryView = false);
        SwitchToHistoryCommand = new RelayCommand(() => IsHistoryView = true);
        ToggleViewModeCommand = new RelayCommand(() => IsHistoryView = !IsHistoryView);
        DismissBannerCommand = new RelayCommand(() =>
        {
            SuccessMessage = null;
            ErrorMessage = null;
        });

        SelectPatientCommand = new RelayCommand<PatientDto>(SelectPatient);
        ClearPatientCommand = new RelayCommand(ClearPatient);
        ChangePatientCommand = new RelayCommand(ClearPatient);

        AddOrUpdateInvestigationCommand = new RelayCommand(AddOrUpdateInvestigation);
        EditInvestigationRowCommand = new RelayCommand<SelectedInvestigationItemState>(EditInvestigationRow);
        CancelEditInvestigationCommand = new RelayCommand(CancelEditInvestigation);
        DeleteInvestigationRowCommand = new RelayCommand<SelectedInvestigationItemState>(DeleteInvestigationRow);
        RemoveInvestigationCommand = DeleteInvestigationRowCommand;

        SelectCategoryChipCommand = new RelayCommand<string>(SelectCategoryChip);
        SelectSiteChipCommand = new RelayCommand<string>(SelectSiteChip);
        SelectProtocolChipCommand = new RelayCommand<string>(SelectProtocolChip);

        ClearOrderCommand = new RelayCommand(() => ClearOrder(clearNotifications: true));

        SaveOrderCommand = new AsyncRelayCommand(SaveOrderAsync);
        PrintPreviewOrderCommand = new AsyncRelayCommand(PrintPreviewOrderAsync);
        PrintOrderDirectCommand = new AsyncRelayCommand(PrintOrderDirectAsync);

        RefreshHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);
        ClearHistoryFilterCommand = new RelayCommand(() =>
        {
            HistorySearchQuery = string.Empty;
            HistoryFromDate = null;
            HistoryToDate = null;
            _ = LoadHistoryAsync();
        });
        SelectHistoryRequestCommand = new RelayCommand<InvestigationRequestSummaryDto>(req =>
        {
            if (req != null) SelectedHistoryRequest = req;
        });
        PrintHistoryDetailCommand = new RelayCommand(PrintHistoryDetail);
        PreviewHistoryDetailCommand = new RelayCommand(PreviewHistoryDetail);

        PatientSearchResults.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPatientSearchResults));
            OnPropertyChanged(nameof(SearchResultCountText));
        };

        SelectedInvestigations.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedInvestigationCount));
            OnPropertyChanged(nameof(HasSelectedInvestigations));
            OnPropertyChanged(nameof(ItemsCount));
            OnPropertyChanged(nameof(HasOrderItems));
            OnPropertyChanged(nameof(OrderCountSummary));
        };
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        await LoadRecentPatientsAsync();

        if (parameter is PatientDto patientParam)
        {
            SelectPatient(patientParam);
        }
        else if (parameter is int patientIdParam)
        {
            var p = await _patientService.GetPatientByIdAsync(patientIdParam);
            if (p != null) SelectPatient(p);
        }
    }

    #region Direct Test Input Editor Logic

    public void AddOrUpdateInvestigation()
    {
        if (string.IsNullOrWhiteSpace(EditorInvestigationName))
        {
            EditorErrorMessage = "Please enter the investigation or test name (e.g. Chest X-Ray, CBC, USG Abdomen).";
            return;
        }

        var cat = string.IsNullOrWhiteSpace(EditorCategory) ? "Laboratory" : EditorCategory.Trim();

        if (IsEditingItem && _editingItem != null)
        {
            _editingItem.InvestigationName = EditorInvestigationName.Trim();
            _editingItem.Category = cat;
            _editingItem.BodyPartOrSite = EditorBodyPartOrSite?.Trim();
            _editingItem.ViewOrProtocol = EditorViewOrProtocol?.Trim();
            _editingItem.SpecificInstructions = EditorSpecificInstructions?.Trim();

            SuccessMessage = $"Updated \"{_editingItem.InvestigationName}\" in order.";
            CancelEditInvestigation();
            return;
        }

        // Add new test
        var newItem = new SelectedInvestigationItemState
        {
            InvestigationName = EditorInvestigationName.Trim(),
            Category = cat,
            BodyPartOrSite = EditorBodyPartOrSite?.Trim(),
            ViewOrProtocol = EditorViewOrProtocol?.Trim(),
            SpecificInstructions = EditorSpecificInstructions?.Trim(),
            DisplayOrder = SelectedInvestigations.Count + 1
        };

        SelectedInvestigations.Add(newItem);
        ResetEditorFields();
        ErrorMessage = null;
        EditorErrorMessage = null;
    }

    public void EditInvestigationRow(SelectedInvestigationItemState? item)
    {
        if (item == null) return;
        _editingItem = item;
        IsEditingItem = true;
        EditorInvestigationName = item.InvestigationName;
        EditorCategory = item.Category;
        EditorBodyPartOrSite = item.BodyPartOrSite;
        EditorViewOrProtocol = item.ViewOrProtocol;
        EditorSpecificInstructions = item.SpecificInstructions;
        EditorErrorMessage = null;
    }

    public void CancelEditInvestigation()
    {
        _editingItem = null;
        IsEditingItem = false;
        ResetEditorFields();
    }

    private void ResetEditorFields()
    {
        EditorInvestigationName = string.Empty;
        EditorBodyPartOrSite = string.Empty;
        EditorViewOrProtocol = string.Empty;
        EditorSpecificInstructions = string.Empty;
        EditorErrorMessage = null;
    }

    private void DeleteInvestigationRow(SelectedInvestigationItemState? item)
    {
        if (item == null) return;

        if (ReferenceEquals(_editingItem, item))
        {
            CancelEditInvestigation();
        }

        SelectedInvestigations.Remove(item);

        // Re-number remaining items
        int order = 1;
        foreach (var inv in SelectedInvestigations)
        {
            inv.DisplayOrder = order++;
        }

        OnPropertyChanged(nameof(SelectedInvestigationCount));
        OnPropertyChanged(nameof(HasSelectedInvestigations));
        OnPropertyChanged(nameof(OrderCountSummary));
    }

    private void SelectCategoryChip(string? category)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            EditorCategory = category;
        }
    }

    private void SelectSiteChip(string? site)
    {
        if (string.IsNullOrWhiteSpace(site)) return;
        if (string.IsNullOrWhiteSpace(EditorBodyPartOrSite))
        {
            EditorBodyPartOrSite = site;
        }
        else
        {
            EditorBodyPartOrSite = $"{EditorBodyPartOrSite}, {site}";
        }
    }

    private void SelectProtocolChip(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol)) return;
        if (string.IsNullOrWhiteSpace(EditorViewOrProtocol))
        {
            EditorViewOrProtocol = protocol;
        }
        else
        {
            EditorViewOrProtocol = $"{EditorViewOrProtocol}, {protocol}";
        }
    }

    public void ClearOrder(bool clearNotifications = true)
    {
        SelectedInvestigations.Clear();
        CancelEditInvestigation();
        ClinicalNotes = string.Empty;
        GeneralInstructions = string.Empty;
        if (clearNotifications)
        {
            ErrorMessage = null;
            SuccessMessage = null;
        }

        OnPropertyChanged(nameof(SelectedInvestigationCount));
        OnPropertyChanged(nameof(HasSelectedInvestigations));
        OnPropertyChanged(nameof(OrderCountSummary));
        OnPropertyChanged(nameof(ItemsCount));
        OnPropertyChanged(nameof(HasOrderItems));
    }

    #endregion

    #region Patient Selection

    public async Task LoadRecentPatientsAsync()
    {
        try
        {
            var recents = await _patientService.GetRecentPatientsAsync(8);
            PatientSearchResults.Clear();
            foreach (var p in recents)
            {
                PatientSearchResults.Add(p);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load recent patients.");
        }
    }

    private void TriggerDebouncedPatientSearch()
    {
        _patientSearchCts?.Cancel();
        _patientSearchCts = new CancellationTokenSource();
        var token = _patientSearchCts.Token;

        Task.Delay(250, token).ContinueWith(async t =>
        {
            if (t.IsCanceled) return;

            try
            {
                var q = PatientSearchQuery;
                IReadOnlyList<PatientDto> results;

                if (string.IsNullOrWhiteSpace(q))
                {
                    results = await _patientService.GetRecentPatientsAsync(8, token);
                }
                else
                {
                    results = await _patientService.SearchPatientsAsync(q, 15, false, token);
                }

                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    PatientSearchResults.Clear();
                    foreach (var p in results)
                    {
                        PatientSearchResults.Add(p);
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error performing patient search.");
            }
        }, TaskScheduler.Default);
    }

    private void SelectPatient(PatientDto? patient)
    {
        if (patient == null) return;
        SelectedPatient = patient;
        ErrorMessage = null;
    }

    private void ClearPatient()
    {
        SelectedPatient = null;
        PatientSearchQuery = string.Empty;
        _ = LoadRecentPatientsAsync();
    }

    #endregion

    #region Order Persistence & Printing

    public async Task<InvestigationRequestDetailDto?> SaveOrderInternalAsync()
    {
        if (SelectedPatient == null)
        {
            ErrorMessage = "Please select a registered patient before saving or printing.";
            return null;
        }

        if (SelectedInvestigations.Count == 0)
        {
            ErrorMessage = "Please add at least one investigation or diagnostic test to the order.";
            return null;
        }

        var createDto = new CreateInvestigationRequestDto
        {
            PatientId = SelectedPatient.Id,
            RequestDate = RequestDate,
            ClinicalNotes = ClinicalNotes?.Trim(),
            Instructions = GeneralInstructions?.Trim(),
            Items = SelectedInvestigations.Select(i => new CreateInvestigationRequestItemDto
            {
                CatalogItemId = i.CatalogItemId,
                InvestigationName = i.InvestigationName.Trim(),
                Category = i.Category.Trim(),
                Subcategory = i.Subcategory?.Trim(),
                BodyPartOrSite = i.BodyPartOrSite?.Trim(),
                ViewOrProtocol = i.ViewOrProtocol?.Trim(),
                SpecificInstructions = i.SpecificInstructions?.Trim(),
                DisplayOrder = i.DisplayOrder
            }).ToList()
        };

        var result = await _investigationService.CreateInvestigationRequestAsync(createDto);
        if (!result.IsSuccess || result.Value == null)
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to save diagnostic investigation request.";
            return null;
        }

        ErrorMessage = null;
        return result.Value;
    }

    public async Task SaveOrderAsync()
    {
        var saved = await SaveOrderInternalAsync();
        if (saved != null)
        {
            SuccessMessage = $"Investigation request #{saved.RequestNumber} with {saved.Items.Count} test(s) successfully saved for {saved.PatientName}.";
            ErrorMessage = null;
        }
    }

    public async Task PrintPreviewOrderAsync()
    {
        var saved = await SaveOrderInternalAsync();
        if (saved != null)
        {
            LaunchPrintPreview(saved);
            SuccessMessage = $"Investigation request #{saved.RequestNumber} generated and preview launched for {saved.PatientName}.";
            ErrorMessage = null;
        }
    }

    public async Task PrintOrderDirectAsync()
    {
        var saved = await SaveOrderInternalAsync();
        if (saved != null)
        {
            LaunchPrintPreview(saved);
            SuccessMessage = $"Investigation request #{saved.RequestNumber} ready for printing for {saved.PatientName}.";
            ErrorMessage = null;
        }
    }

    private void LaunchPrintPreview(InvestigationRequestDetailDto request)
    {
        try
        {
            if (_printLauncher != null)
            {
                _printLauncher.Launch(request);
                return;
            }

            var app = System.Windows.Application.Current;
            if (app != null)
            {
                if (!app.Dispatcher.CheckAccess())
                {
                    app.Dispatcher.Invoke(() =>
                    {
                        var preview = new Views.InvestigationPrintPreviewWindow(request);
                        preview.ShowDialog();
                    });
                    return;
                }

                var window = new Views.InvestigationPrintPreviewWindow(request);
                window.ShowDialog();
                return;
            }

            _dialogService.ShowInformation("Print Preview", $"Preview loaded for order #{request.RequestNumber}.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to launch print preview window.");
            _dialogService.ShowError("Print Error", $"Unable to open print preview: {ex.Message}");
        }
    }

    #endregion

    #region History

    public async Task LoadHistoryAsync()
    {
        IsLoadingHistory = true;
        try
        {
            var requests = await _investigationService.GetRecentRequestsAsync(60);
            HistoryRequests.Clear();
            foreach (var r in requests)
            {
                HistoryRequests.Add(r);
            }

            if (HistoryRequests.Count > 0 && SelectedHistoryRequest == null)
            {
                SelectedHistoryRequest = HistoryRequests[0];
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load investigation history.");
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    public async Task SearchHistoryAsync()
    {
        IsLoadingHistory = true;
        try
        {
            var results = await _investigationService.SearchRequestsAsync(
                HistorySearchQuery,
                HistoryFromDate,
                HistoryToDate,
                null,
                80);

            HistoryRequests.Clear();
            foreach (var r in results)
            {
                HistoryRequests.Add(r);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error searching investigation history.");
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    private async Task LoadHistoryDetailAsync(int requestId)
    {
        try
        {
            var detail = await _investigationService.GetRequestByIdAsync(requestId);
            SelectedHistoryDetail = detail;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load investigation detail for ID {Id}", requestId);
        }
    }

    private void PreviewHistoryDetail()
    {
        if (SelectedHistoryDetail != null)
        {
            LaunchPrintPreview(SelectedHistoryDetail);
        }
    }

    private void PrintHistoryDetail()
    {
        if (SelectedHistoryDetail != null)
        {
            LaunchPrintPreview(SelectedHistoryDetail);
        }
    }

    #endregion
}
