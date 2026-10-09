using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Application.Interfaces;

public interface IAutoBackupService
{
    BackupSettings GetSettings();
    Task SaveSettingsAsync(BackupSettings settings, CancellationToken cancellationToken = default);
    BackupStatus GetStatus();
    Task<bool> ShouldRunShutdownBackupAsync(CancellationToken cancellationToken = default);
    Task<BackupResult> RunAutoBackupAsync(CancellationToken cancellationToken = default);
    Task PruneRetentionAsync(CancellationToken cancellationToken = default);
    bool IsDestinationOnSameDriveAsDatabase(string? destinationDirectory);
}
