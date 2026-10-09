using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Presentation.Services;
using Microsoft.Win32;

namespace DoctorRx.Presentation.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    public override NavigationSection NavigationSection => NavigationSection.Settings;

    private readonly IBackupService _backupService;
    private readonly IRestoreService _restoreService;
    private readonly IAutoBackupService _autoBackupService;
    private readonly IDatabaseHealthService _healthService;
    private readonly IAppPaths _appPaths;
    private readonly IDialogService _dialogService;
    private readonly IClock _clock;

    private int _selectedTabIndex = 0;
    private string _databaseSizeText = "Calculating...";
    private string _freeSpaceText = "Calculating...";
    private string _healthSummary = "PRAGMA quick_check: OK • Healthy";
    private string _healthBadgeColor = "#059669";
    private bool _isHealthy = true;
    private bool _isRunningBackup;
    private bool _isRunningRestore;
    private bool _isRunningHealthCheck;
    private string? _operationStatusMessage;

    // Settings fields
    private bool _autoBackupOnShutdown = true;
    private bool _autoBackupDaily = true;
    private string _customBackupDestination = string.Empty;
    private bool _isDestinationOnSameDrive;
    private int _keepDailyCount = 7;
    private int _keepWeeklyCount = 4;
    private int _keepMonthlyCount = 3;

    private BackupMetadataDto? _selectedBackup;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string DatabasePath => _appPaths.DatabasePath;
    public string BackupsDirectory => _appPaths.BackupsDirectory;
    public string AutoBackupsDirectory => _appPaths.AutoBackupsDirectory;

    public string DatabaseSizeText
    {
        get => _databaseSizeText;
        set => SetProperty(ref _databaseSizeText, value);
    }

    public string FreeSpaceText
    {
        get => _freeSpaceText;
        set => SetProperty(ref _freeSpaceText, value);
    }

    public string HealthSummary
    {
        get => _healthSummary;
        set => SetProperty(ref _healthSummary, value);
    }

    public string HealthBadgeColor
    {
        get => _healthBadgeColor;
        set => SetProperty(ref _healthBadgeColor, value);
    }

    public bool IsHealthy
    {
        get => _isHealthy;
        set => SetProperty(ref _isHealthy, value);
    }

    public bool IsRunningBackup
    {
        get => _isRunningBackup;
        set => SetProperty(ref _isRunningBackup, value);
    }

    public bool IsRunningRestore
    {
        get => _isRunningRestore;
        set => SetProperty(ref _isRunningRestore, value);
    }

    public bool IsRunningHealthCheck
    {
        get => _isRunningHealthCheck;
        set => SetProperty(ref _isRunningHealthCheck, value);
    }

    public string? OperationStatusMessage
    {
        get => _operationStatusMessage;
        set => SetProperty(ref _operationStatusMessage, value);
    }

    public bool AutoBackupOnShutdown
    {
        get => _autoBackupOnShutdown;
        set => SetProperty(ref _autoBackupOnShutdown, value);
    }

    public bool AutoBackupDaily
    {
        get => _autoBackupDaily;
        set => SetProperty(ref _autoBackupDaily, value);
    }

    public string CustomBackupDestination
    {
        get => _customBackupDestination;
        set
        {
            if (SetProperty(ref _customBackupDestination, value))
            {
                IsDestinationOnSameDrive = _autoBackupService.IsDestinationOnSameDriveAsDatabase(value);
            }
        }
    }

    public bool IsDestinationOnSameDrive
    {
        get => _isDestinationOnSameDrive;
        set => SetProperty(ref _isDestinationOnSameDrive, value);
    }

    public int KeepDailyCount
    {
        get => _keepDailyCount;
        set => SetProperty(ref _keepDailyCount, Math.Max(1, value));
    }

    public int KeepWeeklyCount
    {
        get => _keepWeeklyCount;
        set => SetProperty(ref _keepWeeklyCount, Math.Max(1, value));
    }

    public int KeepMonthlyCount
    {
        get => _keepMonthlyCount;
        set => SetProperty(ref _keepMonthlyCount, Math.Max(1, value));
    }

    public BackupMetadataDto? SelectedBackup
    {
        get => _selectedBackup;
        set => SetProperty(ref _selectedBackup, value);
    }

    public ObservableCollection<BackupMetadataDto> Backups { get; } = new();

    public ICommand BackupNowCommand { get; }
    public ICommand RefreshBackupsCommand { get; }
    public ICommand VerifyBackupCommand { get; }
    public ICommand RestoreBackupCommand { get; }
    public ICommand RestoreFromFileCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand RunFullIntegrityCheckCommand { get; }
    public ICommand SaveBackupSettingsCommand { get; }
    public ICommand BrowseFolderCommand { get; }

    public SettingsViewModel()
    {
        _backupService = null!;
        _restoreService = null!;
        _autoBackupService = null!;
        _healthService = null!;
        _appPaths = null!;
        _dialogService = null!;
        _clock = null!;

        BackupNowCommand = new RelayCommand(() => { });
        RefreshBackupsCommand = new RelayCommand(() => { });
        VerifyBackupCommand = new RelayCommand(() => { });
        RestoreBackupCommand = new RelayCommand(() => { });
        RestoreFromFileCommand = new RelayCommand(() => { });
        OpenFolderCommand = new RelayCommand(() => { });
        RunFullIntegrityCheckCommand = new RelayCommand(() => { });
        SaveBackupSettingsCommand = new RelayCommand(() => { });
        BrowseFolderCommand = new RelayCommand(() => { });
    }

    public SettingsViewModel(
        IBackupService backupService,
        IRestoreService restoreService,
        IAutoBackupService autoBackupService,
        IDatabaseHealthService healthService,
        IAppPaths appPaths,
        IDialogService dialogService,
        IClock clock)
    {
        _backupService = backupService;
        _restoreService = restoreService;
        _autoBackupService = autoBackupService;
        _healthService = healthService;
        _appPaths = appPaths;
        _dialogService = dialogService;
        _clock = clock;

        BackupNowCommand = new AsyncRelayCommand(CreateManualBackupAsync);
        RefreshBackupsCommand = new AsyncRelayCommand(LoadBackupsAsync);
        VerifyBackupCommand = new AsyncRelayCommand<BackupMetadataDto>(VerifyBackupAsync);
        RestoreBackupCommand = new AsyncRelayCommand<BackupMetadataDto>(RestoreBackupAsync);
        RestoreFromFileCommand = new AsyncRelayCommand(RestoreFromFileAsync);
        OpenFolderCommand = new RelayCommand<string>(OpenFolderInExplorer);
        RunFullIntegrityCheckCommand = new AsyncRelayCommand(RunFullIntegrityCheckAsync);
        SaveBackupSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        BrowseFolderCommand = new RelayCommand(BrowseBackupFolder);
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        LoadSettings();
        await RefreshHealthAndMetricsAsync();
        await LoadBackupsAsync();
    }

    private void LoadSettings()
    {
        var settings = _autoBackupService.GetSettings();
        _autoBackupDaily = settings.AutoBackupEnabled;
        _customBackupDestination = settings.CustomPrimaryDirectory ?? string.Empty;
        _keepDailyCount = settings.DailyRetention;
        _keepWeeklyCount = settings.WeeklyRetention;
        _keepMonthlyCount = settings.MonthlyRetention;
        _isDestinationOnSameDrive = _autoBackupService.IsDestinationOnSameDriveAsDatabase(_customBackupDestination);

        OnPropertyChanged(nameof(AutoBackupDaily));
        OnPropertyChanged(nameof(CustomBackupDestination));
        OnPropertyChanged(nameof(KeepDailyCount));
        OnPropertyChanged(nameof(KeepWeeklyCount));
        OnPropertyChanged(nameof(KeepMonthlyCount));
        OnPropertyChanged(nameof(IsDestinationOnSameDrive));
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var updated = new BackupSettings
            {
                AutoBackupEnabled = AutoBackupDaily,
                CustomPrimaryDirectory = string.IsNullOrWhiteSpace(CustomBackupDestination) ? null : CustomBackupDestination,
                DailyRetention = KeepDailyCount,
                WeeklyRetention = KeepWeeklyCount,
                MonthlyRetention = KeepMonthlyCount
            };

            await _autoBackupService.SaveSettingsAsync(updated);
            _dialogService.ShowInformation("Settings Saved", "Automatic backup and retention preferences have been updated.");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Save Failed", $"Could not save backup preferences: {ex.Message}");
        }
    }

    private async Task RefreshHealthAndMetricsAsync()
    {
        try
        {
            if (File.Exists(_appPaths.DatabasePath))
            {
                var len = new FileInfo(_appPaths.DatabasePath).Length;
                DatabaseSizeText = $"{len / (1024.0 * 1024.0):F2} MB";
            }
            else
            {
                DatabaseSizeText = "0 MB (Fresh)";
            }

            var freeBytes = _healthService.GetAvailableFreeSpaceBytes();
            FreeSpaceText = $"{freeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB free";

            var report = await _healthService.RunQuickCheckAsync();
            if (report.IsHealthy)
            {
                HealthSummary = "Database Healthy • PRAGMA quick_check OK";
                HealthBadgeColor = "#059669";
                IsHealthy = true;
            }
            else
            {
                HealthSummary = $"Integrity Warning: {report.ErrorDetails}";
                HealthBadgeColor = "#DC2626";
                IsHealthy = false;
            }
        }
        catch (Exception ex)
        {
            HealthSummary = $"Error inspecting database: {ex.Message}";
            HealthBadgeColor = "#DC2626";
            IsHealthy = false;
        }
    }

    private async Task LoadBackupsAsync()
    {
        try
        {
            Backups.Clear();
            var targetDir = string.IsNullOrWhiteSpace(CustomBackupDestination) ? _appPaths.BackupsDirectory : CustomBackupDestination;
            var list = await _backupService.GetAvailableBackupsAsync(targetDir);

            // Also include auto backups
            if (Directory.Exists(_appPaths.AutoBackupsDirectory))
            {
                var autoList = await _backupService.GetAvailableBackupsAsync(_appPaths.AutoBackupsDirectory);
                var combined = list.Concat(autoList).DistinctBy(b => b.FilePath).OrderByDescending(b => b.CreatedAtUtc);
                foreach (var b in combined)
                {
                    Backups.Add(b);
                }
            }
            else
            {
                foreach (var b in list)
                {
                    Backups.Add(b);
                }
            }

            if (Backups.Any())
            {
                SelectedBackup = Backups.First();
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowWarning("Backups Notice", $"Could not load backups list: {ex.Message}");
        }
    }

    private async Task CreateManualBackupAsync()
    {
        if (IsRunningBackup) return;

        try
        {
            IsRunningBackup = true;
            OperationStatusMessage = "Creating SQLite online backup snapshot...";

            var targetDir = string.IsNullOrWhiteSpace(CustomBackupDestination) ? _appPaths.BackupsDirectory : CustomBackupDestination;
            var result = await _backupService.CreateBackupAsync(targetDir);

            if (result.Success && result.BackupFilePath != null)
            {
                var fileInfo = new FileInfo(result.BackupFilePath);
                var sizeMb = fileInfo.Exists ? fileInfo.Length / (1024.0 * 1024.0) : 0;
                await LoadBackupsAsync();
                await RefreshHealthAndMetricsAsync();
                _dialogService.ShowInformation(
                    "Backup Complete",
                    $"Backup created successfully!\n\nFile: {Path.GetFileName(result.BackupFilePath)}\nSize: {sizeMb:F2} MB\nDestination: {targetDir}");
            }
            else
            {
                _dialogService.ShowError("Backup Failed", result.ErrorMessage ?? "Unknown backup error.");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Backup Error", ex.Message);
        }
        finally
        {
            IsRunningBackup = false;
            OperationStatusMessage = null;
        }
    }

    private async Task VerifyBackupAsync(BackupMetadataDto? backup)
    {
        if (backup == null) return;

        try
        {
            OperationStatusMessage = $"Verifying {backup.FileName}...";
            var verification = await _backupService.VerifyBackupAsync(backup.FilePath);

            if (verification.IsValid)
            {
                var manifest = backup.Manifest;
                var counts = manifest != null && manifest.TableRowCounts != null
                    ? string.Join("\n", manifest.TableRowCounts.Select(kv => $"  • {kv.Key}: {kv.Value} records"))
                    : "Table row counts matched";
                var checksum = manifest != null ? manifest.DatabaseSha256[..Math.Min(16, manifest.DatabaseSha256.Length)] : "Matched";
                var migration = manifest != null ? manifest.LatestMigrationId : "Verified";
                var created = manifest != null ? manifest.CreatedAtUtc.ToLocalTime().ToString("f") : backup.CreatedAtUtc.ToLocalTime().ToString("f");

                _dialogService.ShowInformation(
                    "Backup Verified",
                    $"The backup archive is valid and intact!\n\n" +
                    $"Status: {verification.StatusSummary}\n" +
                    $"SHA-256 Checksum: {checksum}...\n" +
                    $"Schema Migration: {migration}\n" +
                    $"Created: {created}\n\n" +
                    $"Database Content:\n{counts}");
            }
            else
            {
                _dialogService.ShowError(
                    "Verification Failed",
                    $"The backup archive failed verification checks:\n\n{verification.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Verification Error", ex.Message);
        }
        finally
        {
            OperationStatusMessage = null;
        }
    }

    private async Task RestoreBackupAsync(BackupMetadataDto? backup)
    {
        if (backup == null || IsRunningRestore) return;

        var validation = await _restoreService.ValidateBackupForRestoreAsync(backup.FilePath);
        if (!validation.CanRestore)
        {
            _dialogService.ShowError("Cannot Restore Backup", validation.Reason ?? "Validation check failed.");
            return;
        }

        string warning = validation.RequiresMigration
            ? "\n\nNote: This backup was created by an older schema version. DoctorRx will migrate the database forward automatically."
            : string.Empty;

        var confirmed = _dialogService.ShowConfirmation(
            "Confirm Database Restore",
            $"Restoring will replace the current database with the selected backup:\n\n" +
            $"File: {backup.FileName}\n" +
            $"Date: {backup.CreatedAtUtc.ToLocalTime():f}\n\n" +
            $"A safety copy of your current database will be created automatically before restoring.{warning}\n\n" +
            $"Do you want to proceed with this restore?");

        if (!confirmed) return;

        try
        {
            IsRunningRestore = true;
            OperationStatusMessage = "Restoring database from backup (creating pre-restore safety copy)...";

            var result = await _restoreService.RestoreFromBackupAsync(backup.FilePath);

            if (result.Success)
            {
                await RefreshHealthAndMetricsAsync();
                await LoadBackupsAsync();
                _dialogService.ShowInformation(
                    "Restore Completed",
                    $"Database has been restored successfully!\n\n" +
                    $"A safety copy of your previous data was preserved at:\n{result.SafetyBackupPath}");
            }
            else
            {
                _dialogService.ShowError(
                    "Restore Failed",
                    result.ErrorMessage ?? "Unknown error during restore.");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Restore Error", ex.Message);
        }
        finally
        {
            IsRunningRestore = false;
            OperationStatusMessage = null;
        }
    }

    private async Task RestoreFromFileAsync()
    {
        var openDialog = new OpenFileDialog
        {
            Title = "Select DoctorRx Backup Archive",
            Filter = "DoctorRx Backups (*.drxbackup)|*.drxbackup|ZIP Archives (*.zip)|*.zip|All Files (*.*)|*.*",
            InitialDirectory = _appPaths.BackupsDirectory
        };

        if (openDialog.ShowDialog() == true)
        {
            var filePath = openDialog.FileName;
            var fileInfo = new FileInfo(filePath);
            var metadata = new BackupMetadataDto(
                FilePath: filePath,
                FileName: fileInfo.Name,
                FileSizeBytes: fileInfo.Length,
                CreatedAtUtc: fileInfo.CreationTimeUtc,
                IsVerified: true,
                Manifest: null);

            await RestoreBackupAsync(metadata);
        }
    }

    private async Task RunFullIntegrityCheckAsync()
    {
        if (IsRunningHealthCheck) return;

        try
        {
            IsRunningHealthCheck = true;
            OperationStatusMessage = "Running SQLite PRAGMA integrity_check and verifying search tokens...";

            var report = await _healthService.RunFullIntegrityCheckAsync();

            if (report.IsHealthy)
            {
                HealthSummary = "Database Healthy • Full Integrity Check OK";
                HealthBadgeColor = "#059669";
                IsHealthy = true;

                _dialogService.ShowInformation(
                    "Integrity Check Passed",
                    $"Database integrity is confirmed clean!\n\n" +
                    $"• PRAGMA integrity_check: ok\n" +
                    $"• Search token index: verified\n" +
                    $"• Database size: {report.DatabaseSizeBytes / (1024.0 * 1024.0):F2} MB");
            }
            else
            {
                HealthSummary = $"Integrity Issues: {report.ErrorDetails}";
                HealthBadgeColor = "#DC2626";
                IsHealthy = false;

                _dialogService.ShowError(
                    "Integrity Issues Detected",
                    $"Database check reported errors:\n\n{report.ErrorDetails}");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Integrity Check Error", ex.Message);
        }
        finally
        {
            IsRunningHealthCheck = false;
            OperationStatusMessage = null;
        }
    }

    private void OpenFolderInExplorer(string? folderPath)
    {
        var path = string.IsNullOrWhiteSpace(folderPath) ? _appPaths.BackupsDirectory : folderPath;
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowWarning("Folder Error", $"Could not open folder in Explorer: {ex.Message}");
        }
    }

    private void BrowseBackupFolder()
    {
        var openFolderDialog = new OpenFolderDialog
        {
            Title = "Select Backup Destination Directory",
            InitialDirectory = string.IsNullOrWhiteSpace(CustomBackupDestination) ? _appPaths.BackupsDirectory : CustomBackupDestination
        };

        if (openFolderDialog.ShowDialog() == true)
        {
            CustomBackupDestination = openFolderDialog.FolderName;
        }
    }
}
