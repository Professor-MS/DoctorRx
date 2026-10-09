using System;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Application.Interfaces;

public record BackupValidationReport(
    bool CanRestore,
    string? Reason,
    BackupManifest? Manifest,
    bool RequiresMigration,
    bool IsNewerVersion
);

public record RestoreResult(
    bool Success,
    string? ErrorMessage,
    string? SafetyBackupPath,
    bool RolledBack
);

public interface IRestoreService
{
    Task<BackupValidationReport> ValidateBackupForRestoreAsync(string backupFilePath, CancellationToken cancellationToken = default);
    Task<RestoreResult> RestoreFromBackupAsync(string backupFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
