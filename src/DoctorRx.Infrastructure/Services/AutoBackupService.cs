using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Infrastructure.Services;

public class AutoBackupService : IAutoBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IBackupService _backupService;
    private readonly IAppPaths _appPaths;
    private readonly IFileSystem _fileSystem;
    private readonly IClock _clock;
    private readonly ILogger<AutoBackupService> _logger;

    private readonly string _settingsFilePath;
    private readonly string _statusFilePath;

    public AutoBackupService(
        IBackupService backupService,
        IAppPaths appPaths,
        IFileSystem fileSystem,
        IClock clock,
        ILogger<AutoBackupService> logger)
    {
        _backupService = backupService;
        _appPaths = appPaths;
        _fileSystem = fileSystem;
        _clock = clock;
        _logger = logger;

        _settingsFilePath = Path.Combine(_appPaths.BaseDirectory, "backup-settings.json");
        _statusFilePath = Path.Combine(_appPaths.BaseDirectory, "backup-status.json");
    }

    public BackupSettings GetSettings()
    {
        try
        {
            if (_fileSystem.FileExists(_settingsFilePath))
            {
                var json = _fileSystem.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<BackupSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load backup settings from {Path}. Using default settings.", _settingsFilePath);
        }

        return new BackupSettings();
    }

    public async Task SaveSettingsAsync(BackupSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            _fileSystem.WriteAllText(_settingsFilePath, json);
            _logger.LogInformation("Backup settings saved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist backup settings to {Path}", _settingsFilePath);
            throw;
        }
    }

    public BackupStatus GetStatus()
    {
        try
        {
            if (_fileSystem.FileExists(_statusFilePath))
            {
                var json = _fileSystem.ReadAllText(_statusFilePath);
                var status = JsonSerializer.Deserialize<BackupStatus>(json);
                if (status != null) return status;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load backup status from {Path}. Initializing fresh status.", _statusFilePath);
        }

        return new BackupStatus();
    }

    public Task<bool> ShouldRunShutdownBackupAsync(CancellationToken cancellationToken = default)
    {
        var settings = GetSettings();
        if (!settings.AutoBackupEnabled)
        {
            return Task.FromResult(false);
        }

        var status = GetStatus();
        if (!status.LastSuccessfulBackupUtc.HasValue)
        {
            return Task.FromResult(true);
        }

        var hoursSinceLast = (_clock.UtcNow - status.LastSuccessfulBackupUtc.Value).TotalHours;
        return Task.FromResult(hoursSinceLast >= settings.AutoBackupIntervalHours);
    }

    public async Task<BackupResult> RunAutoBackupAsync(CancellationToken cancellationToken = default)
    {
        var settings = GetSettings();
        var primaryDir = string.IsNullOrWhiteSpace(settings.CustomPrimaryDirectory)
            ? _appPaths.AutoBackupsDirectory
            : settings.CustomPrimaryDirectory;

        _logger.LogInformation("Executing automatic backup to primary destination {PrimaryDir}", primaryDir);

        var result = await _backupService.CreateBackupAsync(primaryDir, null, cancellationToken);
        var status = GetStatus();
        status.LastAttemptUtc = _clock.UtcNow;

        if (result.Success && result.BackupFilePath != null)
        {
            status.LastSuccessfulBackupUtc = _clock.UtcNow;
            status.LastBackupFilePath = result.BackupFilePath;
            status.LastBackupVerified = result.IsVerified;
            status.LastAttemptSuccess = true;
            status.LastAttemptErrorMessage = null;

            // Secondary destination handling (e.g., USB drive)
            if (!string.IsNullOrWhiteSpace(settings.SecondaryDirectory))
            {
                await HandleSecondaryDestinationAsync(result.BackupFilePath, settings.SecondaryDirectory, cancellationToken);
            }

            // Prune retention according to GFS
            await PruneRetentionAsync(cancellationToken);
        }
        else
        {
            status.LastAttemptSuccess = false;
            status.LastAttemptErrorMessage = result.ErrorMessage;
            _logger.LogError("Automatic backup failed: {ErrorMessage}", result.ErrorMessage);
        }

        SaveStatus(status);
        return result;
    }

    public async Task PruneRetentionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = GetSettings();
            var primaryDir = string.IsNullOrWhiteSpace(settings.CustomPrimaryDirectory)
                ? _appPaths.AutoBackupsDirectory
                : settings.CustomPrimaryDirectory;

            // 1. Prune Auto backups
            var allBackups = await _backupService.GetAvailableBackupsAsync(primaryDir, cancellationToken);
            var plan = BackupRetentionPolicy.EvaluateBackupsToPrune(
                allBackups,
                dailyCount: settings.DailyRetention,
                weeklyCount: settings.WeeklyRetention,
                monthlyCount: settings.MonthlyRetention);

            foreach (var item in plan.BackupsToDelete)
            {
                try
                {
                    _fileSystem.DeleteFile(item.FilePath);
                    _logger.LogInformation("Pruned old automatic backup {FilePath}", item.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to prune backup {FilePath}", item.FilePath);
                }
            }

            // 2. Prune Safety backups
            if (_fileSystem.DirectoryExists(_appPaths.SafetyBackupsDirectory))
            {
                var safetyBackups = await _backupService.GetAvailableBackupsAsync(_appPaths.SafetyBackupsDirectory, cancellationToken);
                var safetyPlan = BackupRetentionPolicy.EvaluateSafetyBackupsToPrune(safetyBackups, maxSafetyBackups: settings.SafetyRetention);

                foreach (var item in safetyPlan.BackupsToDelete)
                {
                    try
                    {
                        _fileSystem.DeleteFile(item.FilePath);
                        _logger.LogInformation("Pruned old safety backup {FilePath}", item.FilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to prune safety backup {FilePath}", item.FilePath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while pruning old backup files");
        }
    }

    public bool IsDestinationOnSameDriveAsDatabase(string? destinationDirectory)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectory)) return true;

        try
        {
            var dbRoot = Path.GetPathRoot(Path.GetFullPath(_appPaths.DatabasePath));
            var destRoot = Path.GetPathRoot(Path.GetFullPath(destinationDirectory));

            if (string.IsNullOrEmpty(dbRoot) || string.IsNullOrEmpty(destRoot)) return true;
            return string.Equals(dbRoot, destRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private async Task HandleSecondaryDestinationAsync(string sourceBackupPath, string secondaryDir, CancellationToken cancellationToken)
    {
        try
        {
            // Detect if drive / destination is reachable
            var root = Path.GetPathRoot(Path.GetFullPath(secondaryDir));
            if (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
            {
                _logger.LogWarning("Secondary backup destination {Dir} is missing or unplugged. Skipping secondary copy.", secondaryDir);
                return;
            }

            _fileSystem.CreateDirectory(secondaryDir);
            var destFileName = Path.GetFileName(sourceBackupPath);
            var targetPath = Path.Combine(secondaryDir, destFileName);

            _fileSystem.CopyFile(sourceBackupPath, targetPath, overwrite: true);
            _logger.LogInformation("Secondary backup copied successfully to {TargetPath}", targetPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write to secondary backup destination {Dir}. Device may be unplugged or read-only.", secondaryDir);
        }
    }

    private void SaveStatus(BackupStatus status)
    {
        try
        {
            var json = JsonSerializer.Serialize(status, JsonOptions);
            _fileSystem.WriteAllText(_statusFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist backup status to {Path}", _statusFilePath);
        }
    }
}
