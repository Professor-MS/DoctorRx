using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Presentation.Services;

namespace DoctorRx.Presentation.ViewModels;

public class NewPrescriptionViewModel : ViewModelBase
{
    public override NavigationSection NavigationSection => NavigationSection.NewPrescription;

    private readonly object _patientSearchLock = new();
    private readonly object _medicineSearchLock = new();

    private readonly IPatientService _patientService;
    private readonly IMedicineService _medicineService;
    private readonly IPrescriptionService _prescriptionService;
    private readonly IDraftService _draftService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly IPrescriptionComposerValidator _validator;
    private readonly IClock _clock;

    // Autosave timers and synchronization
    private readonly DispatcherTimer _debounceTimer;
    private readonly DispatcherTimer _safetyTimer;
    private readonly DispatcherTimer _undoTimer;
    private Task? _inFlightSaveTask;
    private readonly object _saveLock = new();
    private bool _isFinalized;
    private bool _isInitializing;

    // Undo buffer for deleted medicines (8 seconds)
    private readonly Stack<(int Index, PrescriptionMedicineRowState Item)> _undoStack = new();
    private int _undoSecondsRemaining;
    private bool _isUndoAvailable;
    private string _undoBannerText = string.Empty;

    // State identifiers
    private Guid _draftKey = Guid.NewGuid();
    private int? _amendmentParentId;
    private string? _amendmentParentPrescriptionNumber;
    private int _amendmentNumber;
    private string _lastSavedText = "Draft ready";

    // Patient section
    private PatientDto? _selectedPatient;
    private string _patientSearchQuery = string.Empty;
    private CancellationTokenSource? _patientSearchCts;
    private int _patientSearchRequestId;
    private bool _isPatientSearching;
    private bool _isQuickRegisterDrawerOpen;
    private string _newPatientName = string.Empty;
    private string _newPatientAge = string.Empty;
    private Gender _newPatientGender = Gender.Male;
    private string _newPatientPhone = string.Empty;

    // Clinical observations
    private string? _chiefComplaints;
    private string? _bloodPressure;
    private string? _pulseRate;
    private string? _temperature;
    private string? _weightKg;
    private string? _clinicalNotes;

    // Current Medicine Editor state
    private Guid? _editingRowId;
    private int? _selectedMedicineId;
    private string _medicineSearchQuery = string.Empty;
    private bool _isMedicineSearching;
    private string _medicineName = string.Empty;
    private string? _genericName;
    private string _form = string.Empty;
    private string _strength = string.Empty;
    private string _dose = string.Empty;
    private string _frequency = string.Empty;
    private string? _timing;
    private MealRelation _mealRelation = MealRelation.AsDirected;
    private string? _customMealRelationText;
    private string? _withWhat;
    private string _route = string.Empty;
    private string _duration = string.Empty;
    private string? _instructions;
    private bool _addToCatalog;

    // Directives & Follow-up
    private string? _generalAdvice;
    private FollowUpMode _followUpMode = FollowUpMode.None;
    private int _followUpInterval = 7;
    private DateOnly _customFollowUpDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7));

    // Validation state
    private string? _validationErrorMessage;
    private string? _validationWarningMessage;
    private string? _editorErrorMessage;

    public ObservableCollection<PatientDto> PatientSearchResults { get; } = new();
    public ObservableCollection<MedicineDto> MedicineSearchResults { get; } = new();
    public ObservableCollection<PrescriptionMedicineRowState> PrescribedMedicines { get; } = new();

    public Guid DraftKey => _draftKey;
    public bool IsAmending => _amendmentParentId.HasValue;
    public string? AmendmentParentPrescriptionNumber => _amendmentParentPrescriptionNumber;
    public string CorrectionBannerText => $"You are correcting {AmendmentParentPrescriptionNumber}. The original stays on record.";
    public string ScreenTitle => IsAmending ? $"Correct Prescription ({AmendmentParentPrescriptionNumber})" : "Create New Prescription";

    public PatientDto? SelectedPatient
    {
        get => _selectedPatient;
        set
        {
            if (SetProperty(ref _selectedPatient, value))
            {
                OnPropertyChanged(nameof(HasSelectedPatient));
                OnPropertyChanged(nameof(HasAllergiesAlert));
                TriggerAutosave();
            }
        }
    }

    public bool HasSelectedPatient => SelectedPatient != null;
    public bool HasAllergiesAlert => !string.IsNullOrWhiteSpace(SelectedPatient?.KnownAllergies);

    public string PatientSearchQuery
    {
        get => _patientSearchQuery;
        set
        {
            if (SetProperty(ref _patientSearchQuery, value))
            {
                OnPropertyChanged(nameof(HasSearchQuery));
                OnPropertyChanged(nameof(NoPatientFoundText));
                TriggerDebouncedPatientSearch();
            }
        }
    }

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(PatientSearchQuery);
    public bool NoPatientFound => !IsPatientSearching && HasSearchQuery && PatientSearchResults.Count == 0;
    public string NoPatientFoundText => $"No patient found for '{PatientSearchQuery}'";
    public int PatientSearchResultCount => PatientSearchResults.Count;
    public string SearchResultCountText => HasSearchQuery
        ? $"{PatientSearchResults.Count} result{(PatientSearchResults.Count == 1 ? "" : "s")} found"
        : "Recent patients";
    public bool HasPatientSearchResults => PatientSearchResults.Count > 0;
    public bool HasMedicineSearchResults => MedicineSearchResults.Count > 0;
    public bool HasPrescribedMedicines => PrescribedMedicines.Count > 0;

    public bool IsPatientSearching
    {
        get => _isPatientSearching;
        set
        {
            if (SetProperty(ref _isPatientSearching, value))
            {
                OnPropertyChanged(nameof(NoPatientFound));
            }
        }
    }

    public bool IsQuickRegisterDrawerOpen
    {
        get => _isQuickRegisterDrawerOpen;
        set => SetProperty(ref _isQuickRegisterDrawerOpen, value);
    }

    public string NewPatientName
    {
        get => _newPatientName;
        set => SetProperty(ref _newPatientName, value);
    }

    public string NewPatientAge
    {
        get => _newPatientAge;
        set => SetProperty(ref _newPatientAge, value);
    }

    public Gender NewPatientGender
    {
        get => _newPatientGender;
        set => SetProperty(ref _newPatientGender, value);
    }

    public string NewPatientPhone
    {
        get => _newPatientPhone;
        set => SetProperty(ref _newPatientPhone, value);
    }

    // Observations
    public string? ChiefComplaints
    {
        get => _chiefComplaints;
        set { if (SetProperty(ref _chiefComplaints, value)) TriggerAutosave(); }
    }

    public string? BloodPressure
    {
        get => _bloodPressure;
        set { if (SetProperty(ref _bloodPressure, value)) TriggerAutosave(); }
    }

    public string? PulseRate
    {
        get => _pulseRate;
        set { if (SetProperty(ref _pulseRate, value)) TriggerAutosave(); }
    }

    public string? Temperature
    {
        get => _temperature;
        set { if (SetProperty(ref _temperature, value)) TriggerAutosave(); }
    }

    public string? WeightKg
    {
        get => _weightKg;
        set { if (SetProperty(ref _weightKg, value)) TriggerAutosave(); }
    }

    public string? ClinicalNotes
    {
        get => _clinicalNotes;
        set { if (SetProperty(ref _clinicalNotes, value)) TriggerAutosave(); }
    }

    // Medicine Editor
    public Guid? EditingRowId
    {
        get => _editingRowId;
        set
        {
            if (SetProperty(ref _editingRowId, value))
            {
                OnPropertyChanged(nameof(IsEditingMedicine));
                OnPropertyChanged(nameof(EditorHeaderTitle));
                OnPropertyChanged(nameof(AddOrUpdateMedicineButtonText));
            }
        }
    }

    public bool IsEditingMedicine => EditingRowId.HasValue;
    public string EditorHeaderTitle => IsEditingMedicine ? "Edit Prescribed Medicine" : "Add Medicine (No Default Clinical Values)";
    public string AddOrUpdateMedicineButtonText => IsEditingMedicine ? "Update Medicine" : "Add to Prescription (Enter)";

    public string MedicineSearchQuery
    {
        get => _medicineName;
        set => MedicineName = value;
    }

    public bool IsMedicineSearching
    {
        get => _isMedicineSearching;
        set => SetProperty(ref _isMedicineSearching, value);
    }

    public string MedicineName
    {
        get => _medicineName;
        set
        {
            if (SetProperty(ref _medicineName, value))
            {
                _medicineSearchQuery = value;
                OnPropertyChanged(nameof(MedicineSearchQuery));
                if (EditorErrorMessage != null)
                {
                    EditorErrorMessage = null;
                    ValidationErrorMessage = null;
                }
                _ = SearchMedicinesAsync(value);
            }
        }
    }

    public string? GenericName
    {
        get => _genericName;
        set => SetProperty(ref _genericName, value);
    }

    public string Form
    {
        get => _form;
        set => SetProperty(ref _form, value);
    }

    public string Strength
    {
        get => _strength;
        set => SetProperty(ref _strength, value);
    }

    public string Dose
    {
        get => _dose;
        set
        {
            if (SetProperty(ref _dose, value))
            {
                if (EditorErrorMessage != null)
                {
                    EditorErrorMessage = null;
                    ValidationErrorMessage = null;
                }
            }
        }
    }

    public string Frequency
    {
        get => _frequency;
        set
        {
            if (SetProperty(ref _frequency, value))
            {
                if (EditorErrorMessage != null)
                {
                    EditorErrorMessage = null;
                    ValidationErrorMessage = null;
                }
            }
        }
    }

    public string? Timing
    {
        get => _timing;
        set => SetProperty(ref _timing, value);
    }

    public MealRelation MealRelation
    {
        get => _mealRelation;
        set
        {
            if (SetProperty(ref _mealRelation, value))
            {
                OnPropertyChanged(nameof(IsCustomMealRelation));
            }
        }
    }

    public bool IsCustomMealRelation => MealRelation == MealRelation.Other;

    public string? CustomMealRelationText
    {
        get => _customMealRelationText;
        set => SetProperty(ref _customMealRelationText, value);
    }

    public string? WithWhat
    {
        get => _withWhat;
        set => SetProperty(ref _withWhat, value);
    }

    public string Route
    {
        get => _route;
        set => SetProperty(ref _route, value);
    }

    public string Duration
    {
        get => _duration;
        set => SetProperty(ref _duration, value);
    }

    public string? Instructions
    {
        get => _instructions;
        set => SetProperty(ref _instructions, value);
    }

    public bool AddToCatalog
    {
        get => _addToCatalog;
        set => SetProperty(ref _addToCatalog, value);
    }

    // Directives & Follow-up
    public string? GeneralAdvice
    {
        get => _generalAdvice;
        set { if (SetProperty(ref _generalAdvice, value)) TriggerAutosave(); }
    }

    public FollowUpMode FollowUpMode
    {
        get => _followUpMode;
        set
        {
            if (SetProperty(ref _followUpMode, value))
            {
                OnPropertyChanged(nameof(IsIntervalFollowUp));
                OnPropertyChanged(nameof(IsCustomDateFollowUp));
                OnPropertyChanged(nameof(FollowUpPreviewText));
                TriggerAutosave();
            }
        }
    }

    public bool IsIntervalFollowUp => FollowUpMode is FollowUpMode.InDays or FollowUpMode.InWeeks or FollowUpMode.InMonths;
    public bool IsCustomDateFollowUp => FollowUpMode == FollowUpMode.CustomDate;

    public int FollowUpInterval
    {
        get => _followUpInterval;
        set
        {
            if (SetProperty(ref _followUpInterval, value))
            {
                OnPropertyChanged(nameof(FollowUpPreviewText));
                TriggerAutosave();
            }
        }
    }

    public DateOnly CustomFollowUpDate
    {
        get => _customFollowUpDate;
        set
        {
            if (SetProperty(ref _customFollowUpDate, value))
            {
                OnPropertyChanged(nameof(FollowUpPreviewText));
                TriggerAutosave();
            }
        }
    }

    public string FollowUpPreviewText
    {
        get
        {
            var setting = new FollowUpSettingState
            {
                Mode = FollowUpMode,
                Interval = FollowUpInterval,
                CustomDate = CustomFollowUpDate
            };
            return setting.GetDisplayText(_clock.Today);
        }
    }

    public string LastSavedText
    {
        get => _lastSavedText;
        private set => SetProperty(ref _lastSavedText, value);
    }

    public bool IsUndoAvailable
    {
        get => _isUndoAvailable;
        private set => SetProperty(ref _isUndoAvailable, value);
    }

    public string UndoBannerText
    {
        get => _undoBannerText;
        private set => SetProperty(ref _undoBannerText, value);
    }

    public string? ValidationErrorMessage
    {
        get => _validationErrorMessage;
        private set
        {
            if (SetProperty(ref _validationErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationErrorMessage);

    public string? ValidationWarningMessage
    {
        get => _validationWarningMessage;
        private set
        {
            if (SetProperty(ref _validationWarningMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationWarning));
            }
        }
    }

    public bool HasValidationWarning => !string.IsNullOrWhiteSpace(ValidationWarningMessage);

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

    public bool HasPendingUnaddedMedicine =>
        !IsEditingMedicine &&
        (!string.IsNullOrWhiteSpace(MedicineName) ||
         !string.IsNullOrWhiteSpace(Dose) ||
         !string.IsNullOrWhiteSpace(Frequency) ||
         !string.IsNullOrWhiteSpace(GenericName) ||
         !string.IsNullOrWhiteSpace(Form) ||
         !string.IsNullOrWhiteSpace(Strength) ||
         !string.IsNullOrWhiteSpace(Duration) ||
         !string.IsNullOrWhiteSpace(Route) ||
         !string.IsNullOrWhiteSpace(Instructions));

    public event Action<string>? FocusRequested;
    public event Action? ScrollLastMedicineIntoViewRequested;

    public bool HasUnsavedChanges => !_isFinalized && (HasSelectedPatient || PrescribedMedicines.Count > 0 || !string.IsNullOrWhiteSpace(ChiefComplaints) || !string.IsNullOrWhiteSpace(ClinicalNotes) || !string.IsNullOrWhiteSpace(GeneralAdvice) || HasPendingUnaddedMedicine);

    // Commands
    public ICommand SelectPatientCommand { get; }
    public ICommand ChangePatientCommand { get; }
    public ICommand OpenQuickRegisterDrawerCommand { get; }
    public ICommand CloseQuickRegisterDrawerCommand { get; }
    public ICommand SaveQuickRegisterPatientCommand { get; }
    public ICommand RegisterNewPatientFromSearchCommand { get; }

    public ICommand SelectCatalogMedicineCommand { get; }
    public ICommand SelectFormChipCommand { get; }
    public ICommand SelectDoseChipCommand { get; }
    public ICommand SelectFrequencyChipCommand { get; }
    public ICommand SelectDurationChipCommand { get; }
    public ICommand SelectRouteChipCommand { get; }
    public ICommand SelectMealRelationCommand { get; }
    public ICommand SelectInstructionChipCommand { get; }
    public ICommand AddOrUpdateMedicineCommand { get; }
    public ICommand CancelEditMedicineCommand { get; }
    public ICommand EditMedicineRowCommand { get; }
    public ICommand DeleteMedicineRowCommand { get; }
    public ICommand UndoDeleteMedicineCommand { get; }
    public ICommand MoveUpMedicineRowCommand { get; }
    public ICommand MoveDownMedicineRowCommand { get; }

    public ICommand AddAdvicePresetCommand { get; }
    public IAsyncRelayCommand FinalizePrescriptionCommand { get; }
    public IAsyncRelayCommand SaveDraftExplicitCommand { get; }
    public IAsyncRelayCommand CancelOrDiscardCommand { get; }

    public NewPrescriptionViewModel(
        IPatientService patientService,
        IMedicineService medicineService,
        IPrescriptionService prescriptionService,
        IDraftService draftService,
        IDialogService dialogService,
        INavigationService navigationService,
        IPrescriptionComposerValidator validator,
        IClock clock)
    {
        _patientService = patientService;
        _medicineService = medicineService;
        _prescriptionService = prescriptionService;
        _draftService = draftService;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _validator = validator;
        _clock = clock;

        // Timers
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _debounceTimer.Tick += async (s, e) => { _debounceTimer.Stop(); await SaveDraftInternalAsync(); };

        _safetyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _safetyTimer.Tick += async (s, e) => await SaveDraftInternalAsync();
        _safetyTimer.Start();

        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _undoTimer.Tick += OnUndoTimerTick;

        // Thread-safe collection synchronization for background search queries
        BindingOperations.EnableCollectionSynchronization(PatientSearchResults, _patientSearchLock);
        BindingOperations.EnableCollectionSynchronization(MedicineSearchResults, _medicineSearchLock);

        PatientSearchResults.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPatientSearchResults));
            OnPropertyChanged(nameof(PatientSearchResultCount));
            OnPropertyChanged(nameof(SearchResultCountText));
            OnPropertyChanged(nameof(NoPatientFound));
        };

        MedicineSearchResults.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasMedicineSearchResults));
        };

        PrescribedMedicines.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPrescribedMedicines));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        };

        // Command definitions
        SelectPatientCommand = new RelayCommand<PatientDto>(SelectPatient);
        ChangePatientCommand = new RelayCommand(ChangePatient);
        OpenQuickRegisterDrawerCommand = new RelayCommand(() => IsQuickRegisterDrawerOpen = true);
        CloseQuickRegisterDrawerCommand = new RelayCommand(() => IsQuickRegisterDrawerOpen = false);
        SaveQuickRegisterPatientCommand = new AsyncRelayCommand(SaveQuickRegisterPatientAsync);
        RegisterNewPatientFromSearchCommand = new RelayCommand(RegisterNewPatientFromSearch);

        SelectCatalogMedicineCommand = new RelayCommand<MedicineDto>(SelectCatalogMedicine);
        SelectFormChipCommand = new RelayCommand<string>(chip => Form = chip ?? string.Empty);
        SelectDoseChipCommand = new RelayCommand<string>(chip => Dose = chip ?? string.Empty);
        SelectFrequencyChipCommand = new RelayCommand<string>(chip => Frequency = chip ?? string.Empty);
        SelectDurationChipCommand = new RelayCommand<string>(chip => Duration = chip ?? string.Empty);
        SelectRouteChipCommand = new RelayCommand<string>(chip => Route = chip ?? string.Empty);
        SelectMealRelationCommand = new RelayCommand<MealRelation>(mr => MealRelation = mr);
        SelectInstructionChipCommand = new RelayCommand<string>(chip =>
        {
            if (string.IsNullOrWhiteSpace(chip)) return;
            if (string.IsNullOrWhiteSpace(Instructions))
                Instructions = chip;
            else if (!Instructions.Contains(chip, StringComparison.OrdinalIgnoreCase))
                Instructions = $"{Instructions}; {chip}";
        });

        AddOrUpdateMedicineCommand = new RelayCommand(() => AddOrUpdateMedicine());
        CancelEditMedicineCommand = new RelayCommand(() => ClearMedicineEditor());
        EditMedicineRowCommand = new RelayCommand<PrescriptionMedicineRowState>(EditMedicineRow);
        DeleteMedicineRowCommand = new RelayCommand<PrescriptionMedicineRowState>(DeleteMedicineRow);
        UndoDeleteMedicineCommand = new RelayCommand(UndoDeleteMedicine);
        MoveUpMedicineRowCommand = new RelayCommand<PrescriptionMedicineRowState>(MoveUpMedicineRow);
        MoveDownMedicineRowCommand = new RelayCommand<PrescriptionMedicineRowState>(MoveDownMedicineRow);

        AddAdvicePresetCommand = new RelayCommand<string>(AddAdvicePreset);
        FinalizePrescriptionCommand = new AsyncRelayCommand(FinalizePrescriptionAsync);
        SaveDraftExplicitCommand = new AsyncRelayCommand(async () => await SaveDraftInternalAsync(explicitUserSave: true));
        CancelOrDiscardCommand = new AsyncRelayCommand(CancelOrDiscardAsync);

        // Load 10 recent patients initially for composer patient picker
        _patientSearchCts = new CancellationTokenSource();
        var initialRequestId = Interlocked.Increment(ref _patientSearchRequestId);
        _ = SearchPatientsAsync(string.Empty, initialRequestId, _patientSearchCts.Token);
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        _isInitializing = true;
        try
        {
            if (parameter is PatientDto patientDto)
            {
                SelectedPatient = patientDto;
            }
            else if (parameter is Guid draftKey)
            {
                await LoadDraftAsync(draftKey);
            }
            else if (parameter is PrescriptionDetailDto parentDetail)
            {
                // Amendment mode
                _amendmentParentId = parentDetail.Id;
                _amendmentParentPrescriptionNumber = parentDetail.PrescriptionNumber;
                _amendmentNumber = parentDetail.AmendmentNumber;
                OnPropertyChanged(nameof(IsAmending));
                OnPropertyChanged(nameof(AmendmentParentPrescriptionNumber));
                OnPropertyChanged(nameof(CorrectionBannerText));
                OnPropertyChanged(nameof(ScreenTitle));

                var patient = await _patientService.GetPatientByIdAsync(parentDetail.PatientId);
                SelectedPatient = patient;

                ChiefComplaints = parentDetail.ChiefComplaints;
                BloodPressure = parentDetail.BloodPressure;
                PulseRate = parentDetail.PulseRate;
                Temperature = parentDetail.Temperature;
                WeightKg = parentDetail.WeightKg;
                ClinicalNotes = parentDetail.ClinicalNotes;
                GeneralAdvice = parentDetail.GeneralAdvice;

                PrescribedMedicines.Clear();
                foreach (var item in parentDetail.Items)
                {
                    PrescribedMedicines.Add(new PrescriptionMedicineRowState
                    {
                        MedicineId = item.MedicineId,
                        MedicineName = item.MedicineName,
                        GenericName = item.GenericName,
                        Form = item.Form,
                        Strength = item.Strength,
                        Dose = item.Dose,
                        Frequency = item.Frequency,
                        Timing = item.Timing,
                        MealRelation = item.MealRelation,
                        CustomMealRelationText = item.CustomMealRelationText,
                        WithWhat = item.WithWhat,
                        Route = item.Route,
                        Duration = item.Duration,
                        Instructions = item.Instructions,
                        FormattedDirections = item.FormattedDirections
                    });
                }
            }
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task LoadDraftAsync(Guid draftKey)
    {
        var result = await _draftService.GetAsync(draftKey);
        if (!result.IsSuccess || result.Value == null)
        {
            _dialogService.ShowError("Draft Load Error", result.ErrorMessage ?? "Failed to load draft.");
            return;
        }

        var state = result.Value;
        _draftKey = state.DraftKey;

        if (state.PatientId.HasValue)
        {
            SelectedPatient = await _patientService.GetPatientByIdAsync(state.PatientId.Value);
        }

        ChiefComplaints = state.ChiefComplaints;
        BloodPressure = state.BloodPressure;
        PulseRate = state.PulseRate;
        Temperature = state.Temperature;
        WeightKg = state.WeightKg;
        ClinicalNotes = state.ClinicalNotes;
        GeneralAdvice = state.GeneralAdvice;

        FollowUpMode = state.FollowUp.Mode;
        FollowUpInterval = state.FollowUp.Interval;
        CustomFollowUpDate = state.FollowUp.CustomDate;

        PrescribedMedicines.Clear();
        foreach (var item in state.Items)
        {
            item.FormattedDirections = MedicineInstructionFormatter.Format(item);
            PrescribedMedicines.Add(item);
        }

        LastSavedText = $"Loaded draft from {state.UpdatedAtUtc.ToLocalTime():h:mm tt}";
    }

    private void TriggerDebouncedPatientSearch()
    {
        _patientSearchCts?.Cancel();
        _patientSearchCts?.Dispose();
        _patientSearchCts = new CancellationTokenSource();
        var token = _patientSearchCts.Token;
        var requestId = Interlocked.Increment(ref _patientSearchRequestId);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (token.IsCancellationRequested || requestId != _patientSearchRequestId) return;
                await SearchPatientsAsync(_patientSearchQuery, requestId, token);
            }
            catch (OperationCanceledException)
            {
                // Debounce cancelled, ignore
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Unexpected error in debounced patient search");
            }
        }, token);
    }

    private async Task SearchPatientsAsync(string query, int requestId, CancellationToken cancellationToken = default)
    {
        IsPatientSearching = true;
        try
        {
            IReadOnlyList<PatientDto> results;
            if (string.IsNullOrWhiteSpace(query))
            {
                results = await _patientService.GetRecentPatientsAsync(10, cancellationToken);
            }
            else
            {
                results = await _patientService.SearchPatientsAsync(query, maxResults: 50, showArchived: false, cancellationToken: cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested || requestId != _patientSearchRequestId) return;

            UpdateSearchResults(results);
        }
        catch (OperationCanceledException)
        {
            // Cancelled query must NEVER clear or overwrite the list
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error searching patients in prescription composer");
        }
        finally
        {
            if (requestId == _patientSearchRequestId)
            {
                IsPatientSearching = false;
                OnPropertyChanged(nameof(NoPatientFound));
                OnPropertyChanged(nameof(HasPatientSearchResults));
            }
        }
    }

    private void UpdateSearchResults(IReadOnlyList<PatientDto> results)
    {
        lock (_patientSearchLock)
        {
            PatientSearchResults.Clear();
            foreach (var p in results)
            {
                PatientSearchResults.Add(p);
            }
        }
        OnPropertyChanged(nameof(NoPatientFound));
        OnPropertyChanged(nameof(PatientSearchResultCount));
        OnPropertyChanged(nameof(SearchResultCountText));
    }

    private void RegisterNewPatientFromSearch()
    {
        NewPatientName = PatientSearchQuery?.Trim() ?? string.Empty;
        IsQuickRegisterDrawerOpen = true;
    }

    private void SelectPatient(PatientDto? patient)
    {
        if (patient == null) return;
        SelectedPatient = patient;
        _patientSearchQuery = string.Empty;
        OnPropertyChanged(nameof(PatientSearchQuery));
        OnPropertyChanged(nameof(HasSearchQuery));
        lock (_patientSearchLock)
        {
            PatientSearchResults.Clear();
        }
    }

    private void ChangePatient()
    {
        if (PrescribedMedicines.Count > 0)
        {
            var confirm = _dialogService.ShowConfirmation(
                "Change Patient",
                "Medicines have already been added to this prescription. Changing the patient will assign these medicines to the new patient. Proceed?");
            if (!confirm) return;
        }

        SelectedPatient = null;
    }

    private async Task SaveQuickRegisterPatientAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPatientName))
        {
            _dialogService.ShowWarning("Patient Name Required", "Please enter the patient's name.");
            return;
        }

        if (!int.TryParse(NewPatientAge, out var age) || age < 0 || age > 130)
        {
            _dialogService.ShowWarning("Valid Age Required", "Please enter a valid age between 0 and 130.");
            return;
        }

        var dto = new CreatePatientDto
        {
            Name = NewPatientName.Trim(),
            Age = age,
            Gender = NewPatientGender,
            Phone = string.IsNullOrWhiteSpace(NewPatientPhone) ? null : NewPatientPhone.Trim()
        };

        var result = await _patientService.CreatePatientAsync(dto);
        if (result.IsSuccess && result.Value != null)
        {
            SelectedPatient = result.Value;
            IsQuickRegisterDrawerOpen = false;
            NewPatientName = string.Empty;
            NewPatientAge = string.Empty;
            NewPatientPhone = string.Empty;
        }
        else
        {
            _dialogService.ShowError("Registration Failed", result.ErrorMessage ?? "Could not register patient.");
        }
    }

    private async Task SearchMedicinesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            void Clear()
            {
                lock (_medicineSearchLock)
                {
                    MedicineSearchResults.Clear();
                }
            }

            if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(Clear);
            }
            else
            {
                Clear();
            }
            return;
        }

        IsMedicineSearching = true;
        try
        {
            var results = await _medicineService.SearchMedicinesAsync(query);
            lock (_medicineSearchLock)
            {
                MedicineSearchResults.Clear();
                foreach (var m in results)
                {
                    MedicineSearchResults.Add(m);
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error searching medicines in prescription composer");
        }
        finally
        {
            IsMedicineSearching = false;
        }
    }

    private void SelectCatalogMedicine(MedicineDto? catalogMed)
    {
        if (catalogMed == null) return;

        _selectedMedicineId = catalogMed.Id;
        _medicineName = catalogMed.Name;
        _medicineSearchQuery = catalogMed.Name;
        OnPropertyChanged(nameof(MedicineName));
        OnPropertyChanged(nameof(MedicineSearchQuery));
        GenericName = catalogMed.GenericName;
        Form = catalogMed.Form;
        Strength = catalogMed.Strength;

        // Never auto-fill clinical directions: dose, frequency, duration, meal relation stay unselected
        lock (_medicineSearchLock)
        {
            MedicineSearchResults.Clear();
        }
        EditorErrorMessage = null;
        ValidationErrorMessage = null;
        FocusRequested?.Invoke("Dose");
    }

    public bool AddOrUpdateMedicine()
    {
        ValidationErrorMessage = null;
        ValidationWarningMessage = null;
        EditorErrorMessage = null;

        if (string.IsNullOrWhiteSpace(MedicineName))
        {
            EditorErrorMessage = "Medicine name is required.";
            ValidationErrorMessage = EditorErrorMessage;
            FocusRequested?.Invoke("MedicineName");
            return false;
        }
        if (string.IsNullOrWhiteSpace(Dose))
        {
            EditorErrorMessage = "Dose is required (e.g. 1 tab, 5 ml, 1 drop).";
            ValidationErrorMessage = EditorErrorMessage;
            FocusRequested?.Invoke("Dose");
            return false;
        }
        if (string.IsNullOrWhiteSpace(Frequency))
        {
            EditorErrorMessage = "Frequency is required (e.g. Once daily, Twice daily, Three times daily).";
            ValidationErrorMessage = EditorErrorMessage;
            FocusRequested?.Invoke("Frequency");
            return false;
        }

        // Missing Form is a WARNING, not an error (Amendment 4)
        if (string.IsNullOrWhiteSpace(Form))
        {
            ValidationWarningMessage = "Form is omitted (e.g. Tablet, Syrup). You can proceed without it.";
        }

        var row = new PrescriptionMedicineRowState
        {
            RowId = EditingRowId ?? Guid.NewGuid(),
            MedicineId = _selectedMedicineId,
            MedicineName = MedicineName.Trim(),
            GenericName = GenericName?.Trim(),
            Form = Form?.Trim() ?? string.Empty,
            Strength = Strength?.Trim() ?? string.Empty,
            Dose = Dose.Trim(),
            Frequency = Frequency.Trim(),
            Timing = Timing?.Trim(),
            MealRelation = MealRelation,
            CustomMealRelationText = CustomMealRelationText?.Trim(),
            WithWhat = WithWhat?.Trim(),
            Route = Route?.Trim() ?? string.Empty,
            Duration = Duration?.Trim() ?? string.Empty,
            Instructions = Instructions?.Trim(),
            AddToCatalog = AddToCatalog
        };

        row.FormattedDirections = MedicineInstructionFormatter.Format(row);

        if (EditingRowId.HasValue)
        {
            var existingIndex = PrescribedMedicines.ToList().FindIndex(m => m.RowId == EditingRowId.Value);
            if (existingIndex >= 0)
            {
                PrescribedMedicines[existingIndex] = row;
            }
            else
            {
                PrescribedMedicines.Add(row);
            }
        }
        else
        {
            PrescribedMedicines.Add(row);
        }

        ClearMedicineEditor(preserveWarning: true);
        ValidationErrorMessage = null;
        EditorErrorMessage = null;

        FocusRequested?.Invoke("MedicineName");
        ScrollLastMedicineIntoViewRequested?.Invoke();
        TriggerAutosave();
        return true;
    }

    private void ClearMedicineEditor(bool preserveWarning = false)
    {
        EditingRowId = null;
        _selectedMedicineId = null;
        _medicineName = string.Empty;
        _medicineSearchQuery = string.Empty;
        OnPropertyChanged(nameof(MedicineName));
        OnPropertyChanged(nameof(MedicineSearchQuery));
        MedicineSearchResults.Clear();
        GenericName = null;
        Form = string.Empty;
        Strength = string.Empty;
        Dose = string.Empty;
        Frequency = string.Empty;
        Timing = null;
        MealRelation = MealRelation.AsDirected;
        CustomMealRelationText = null;
        WithWhat = null;
        Route = string.Empty;
        Duration = string.Empty;
        Instructions = null;
        AddToCatalog = false;

        EditorErrorMessage = null;
        ValidationErrorMessage = null;
        if (!preserveWarning)
        {
            ValidationWarningMessage = null;
        }
    }

    private void EditMedicineRow(PrescriptionMedicineRowState? row)
    {
        if (row == null) return;

        EditingRowId = row.RowId;
        _selectedMedicineId = row.MedicineId;
        _medicineName = row.MedicineName;
        _medicineSearchQuery = row.MedicineName;
        OnPropertyChanged(nameof(MedicineName));
        OnPropertyChanged(nameof(MedicineSearchQuery));
        GenericName = row.GenericName;
        Form = row.Form;
        Strength = row.Strength;
        Dose = row.Dose;
        Frequency = row.Frequency;
        Timing = row.Timing;
        MealRelation = row.MealRelation;
        CustomMealRelationText = row.CustomMealRelationText;
        WithWhat = row.WithWhat;
        Route = row.Route;
        Duration = row.Duration;
        Instructions = row.Instructions;
        AddToCatalog = row.AddToCatalog;

        EditorErrorMessage = null;
        ValidationErrorMessage = null;
        ValidationWarningMessage = null;

        FocusRequested?.Invoke("Dose");
    }

    public bool HandlePendingUnaddedMedicineGuard()
    {
        if (!HasPendingUnaddedMedicine) return true;

        var choice = _dialogService.ShowConfirmationWithCancel(
            "Unsaved Medicine in Editor",
            "You have a medicine in the editor that has not been added.",
            "Add medicine",
            "Discard editor",
            "Cancel");

        if (choice == null)
        {
            return false;
        }

        if (choice == true)
        {
            var added = AddOrUpdateMedicine();
            return added;
        }

        ClearMedicineEditor();
        return true;
    }

    private void DeleteMedicineRow(PrescriptionMedicineRowState? row)
    {
        if (row == null) return;

        var index = PrescribedMedicines.IndexOf(row);
        if (index >= 0)
        {
            PrescribedMedicines.RemoveAt(index);
            _undoStack.Push((index, row));

            _undoSecondsRemaining = 8;
            IsUndoAvailable = true;
            UndoBannerText = $"Deleted '{row.MedicineName}'. Undo ({_undoSecondsRemaining}s)";
            _undoTimer.Start();

            TriggerAutosave();
        }
    }

    private void OnUndoTimerTick(object? sender, EventArgs e)
    {
        _undoSecondsRemaining--;
        if (_undoSecondsRemaining <= 0)
        {
            _undoTimer.Stop();
            IsUndoAvailable = false;
            _undoStack.Clear();
        }
        else
        {
            if (_undoStack.Count > 0)
            {
                var (_, item) = _undoStack.Peek();
                UndoBannerText = $"Deleted '{item.MedicineName}'. Undo ({_undoSecondsRemaining}s)";
            }
        }
    }

    private void UndoDeleteMedicine()
    {
        if (_undoStack.Count > 0)
        {
            var (index, item) = _undoStack.Pop();
            if (index >= 0 && index <= PrescribedMedicines.Count)
            {
                PrescribedMedicines.Insert(index, item);
            }
            else
            {
                PrescribedMedicines.Add(item);
            }

            _undoTimer.Stop();
            IsUndoAvailable = false;
            TriggerAutosave();
        }
    }

    private void MoveUpMedicineRow(PrescriptionMedicineRowState? row)
    {
        if (row == null) return;
        var index = PrescribedMedicines.IndexOf(row);
        if (index > 0)
        {
            PrescribedMedicines.Move(index, index - 1);
            TriggerAutosave();
        }
    }

    private void MoveDownMedicineRow(PrescriptionMedicineRowState? row)
    {
        if (row == null) return;
        var index = PrescribedMedicines.IndexOf(row);
        if (index >= 0 && index < PrescribedMedicines.Count - 1)
        {
            PrescribedMedicines.Move(index, index + 1);
            TriggerAutosave();
        }
    }

    private void AddAdvicePreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset)) return;

        if (string.IsNullOrWhiteSpace(GeneralAdvice))
        {
            GeneralAdvice = preset;
        }
        else
        {
            GeneralAdvice += "\n" + preset;
        }
    }

    private void TriggerAutosave()
    {
        if (_isInitializing || _isFinalized) return;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public async Task SaveDraftInternalAsync(bool explicitUserSave = false)
    {
        if (_isFinalized || _isInitializing) return;

        if (explicitUserSave && !HandlePendingUnaddedMedicineGuard()) return;

        // Serialize autosaves: one at a time, last write wins (Amendment 8)
        lock (_saveLock)
        {
            if (_inFlightSaveTask != null && !_inFlightSaveTask.IsCompleted)
            {
                return;
            }
        }

        var state = BuildComposerState();
        var saveTask = Task.Run(async () =>
        {
            var res = await _draftService.SaveAsync(state);
            if (res.IsSuccess)
            {
                LastSavedText = $"Draft autosaved at {DateTime.Now:h:mm:ss tt}";
            }
            else if (explicitUserSave)
            {
                _dialogService.ShowError("Save Draft", res.ErrorMessage ?? "Failed to save draft.");
            }
        });

        lock (_saveLock)
        {
            _inFlightSaveTask = saveTask;
        }

        await saveTask;
    }

    private PrescriptionComposerState BuildComposerState()
    {
        return new PrescriptionComposerState
        {
            DraftKey = _draftKey,
            PatientId = SelectedPatient?.Id,
            PatientName = SelectedPatient?.Name,
            PatientRecordNumber = SelectedPatient?.RecordNumber,
            PatientAgeText = SelectedPatient != null ? $"{SelectedPatient.Age} years" : null,
            PatientGender = SelectedPatient?.Gender,
            PatientPhone = SelectedPatient?.Phone,
            PatientKnownAllergies = SelectedPatient?.KnownAllergies,
            PatientLastVisitDate = SelectedPatient?.LastVisitDate,
            VisitDate = _clock.Today,
            ChiefComplaints = ChiefComplaints,
            BloodPressure = BloodPressure,
            PulseRate = PulseRate,
            Temperature = Temperature,
            WeightKg = WeightKg,
            ClinicalNotes = ClinicalNotes,
            GeneralAdvice = GeneralAdvice,
            FollowUp = new FollowUpSettingState
            {
                Mode = FollowUpMode,
                Interval = FollowUpInterval,
                CustomDate = CustomFollowUpDate
            },
            Items = PrescribedMedicines.ToList(),
            IsFinalized = _isFinalized
        };
    }

    private async Task FinalizePrescriptionAsync()
    {
        if (!HandlePendingUnaddedMedicineGuard()) return;

        ValidationErrorMessage = null;
        ValidationWarningMessage = null;

        var state = BuildComposerState();
        var validation = _validator.Validate(state);

        if (!validation.IsValid)
        {
            ValidationErrorMessage = string.Join("\n", validation.Errors.Select(e => e.Message));
            _dialogService.ShowError("Cannot Finalize Prescription", ValidationErrorMessage);
            return;
        }

        // If there are warnings (e.g. omitted Form per Amendment 4), confirm with doctor
        if (validation.HasWarnings)
        {
            var warningText = string.Join("\n", validation.Warnings.Select(w => w.Message));
            var proceed = _dialogService.ShowConfirmation(
                "Prescription Warnings",
                $"Please note the following warnings:\n\n{warningText}\n\nDo you wish to proceed and finalize this prescription?");
            if (!proceed) return;
        }

        var confirmFinalize = _dialogService.ShowConfirmation(
            "Issue Prescription",
            "Once issued, this prescription cannot be edited. Corrections are made as a new corrected copy.\n\nDo you want to finalize and issue this prescription?");
        if (!confirmFinalize) return;

        // Zombie draft prevention (Amendment 1): stop timers, await in-flight save, mark composer finalized
        _debounceTimer.Stop();
        _safetyTimer.Stop();
        _isFinalized = true;

        if (_inFlightSaveTask != null)
        {
            try { await _inFlightSaveTask; } catch { }
        }

        _draftService.MarkFinalized(_draftKey);

        var dto = new CreatePrescriptionDto
        {
            DraftKey = _draftKey,
            PatientId = SelectedPatient!.Id,
            PrescriptionDate = _clock.Today,
            ChiefComplaints = ChiefComplaints,
            BloodPressure = BloodPressure,
            PulseRate = PulseRate,
            Temperature = Temperature,
            WeightKg = WeightKg,
            ClinicalNotes = ClinicalNotes,
            GeneralAdvice = GeneralAdvice,
            FollowUpDate = state.FollowUp.CalculateDate(_clock.Today),
            FollowUpText = FollowUpPreviewText,
            Items = PrescribedMedicines.Select((m, i) => new CreatePrescriptionMedicineDto
            {
                MedicineId = m.MedicineId,
                MedicineName = m.MedicineName,
                GenericName = m.GenericName,
                Form = m.Form,
                Strength = m.Strength,
                Dose = m.Dose,
                Frequency = m.Frequency,
                Timing = m.Timing,
                MealRelation = m.MealRelation,
                CustomMealRelationText = m.CustomMealRelationText,
                WithWhat = m.WithWhat,
                Route = m.Route,
                Duration = m.Duration,
                Instructions = m.Instructions,
                SortOrder = i + 1,
                AddToCatalog = m.AddToCatalog
            }).ToList()
        };

        Result<PrescriptionDetailDto> result;
        if (IsAmending && _amendmentParentId.HasValue)
        {
            result = await _prescriptionService.AmendPrescriptionAsync(_amendmentParentId.Value, dto);
        }
        else
        {
            result = await _prescriptionService.FinalizePrescriptionAsync(dto);
        }

        if (result.IsSuccess && result.Value != null)
        {
            _dialogService.ShowInformation(
                "Prescription Finalized",
                $"Prescription {result.Value.PrescriptionNumber} has been finalized successfully and locked against tampering.");

            _navigationService.NavigateTo(NavigationDestination.PrescriptionDetail, result.Value);
        }
        else
        {
            _isFinalized = false; // Re-enable if finalize failed so doctor can fix and retry
            _safetyTimer.Start();
            _dialogService.ShowError("Finalization Failed", result.ErrorMessage ?? "Unable to finalize prescription.");
        }
    }

    private async Task CancelOrDiscardAsync()
    {
        if (!HandlePendingUnaddedMedicineGuard()) return;

        var confirm = _dialogService.ShowConfirmation(
            "Discard Draft",
            "Are you sure you want to discard this prescription draft? Any unsaved edits will be lost.");

        if (!confirm) return;

        _debounceTimer.Stop();
        _safetyTimer.Stop();
        _isFinalized = true;

        await _draftService.DiscardAsync(_draftKey);
        _navigationService.NavigateTo(NavigationDestination.Dashboard);
    }
}
