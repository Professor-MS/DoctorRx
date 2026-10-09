using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Application.Interfaces;

public interface IBackupService
{
    Task<BackupResult> CreateBackupAsync(string targetDirectory, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<BackupVerificationResult> VerifyBackupAsync(string backupFilePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupMetadataDto>> GetAvailableBackupsAsync(string directory, CancellationToken cancellationToken = default);
}
