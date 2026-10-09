using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Infrastructure.Services;

public class RestoreService : IRestoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IDbContextFactory<DoctorRxDbContext> _contextFactory;
    private readonly IAppPaths _appPaths;
    private readonly IFileSystem _fileSystem;
    private readonly IBackupService _backupService;
    private readonly IDatabaseMigrator _databaseMigrator;
    private readonly IClock _clock;
    private readonly ILogger<RestoreService> _logger;

    public RestoreService(
        IDbContextFactory<DoctorRxDbContext> contextFactory,
        IAppPaths appPaths,
        IFileSystem fileSystem,
        IBackupService backupService,
        IDatabaseMigrator databaseMigrator,
        IClock clock,
        ILogger<RestoreService> logger)
    {
        _contextFactory = contextFactory;
        _appPaths = appPaths;
        _fileSystem = fileSystem;
        _backupService = backupService;
        _databaseMigrator = databaseMigrator;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BackupValidationReport> ValidateBackupForRestoreAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.FileExists(backupFilePath))
        {
            return new BackupValidationReport(false, "Backup file does not exist.", null, false, false);
        }

        // 1. Verify archive structure, integrity, checksums and manifest
        var verification = await _backupService.VerifyBackupAsync(backupFilePath, cancellationToken);
        if (!verification.IsValid)
        {
            return new BackupValidationReport(false, $"Backup validation failed: {verification.ErrorMessage}", null, false, false);
        }

        // 2. Read manifest
        BackupManifest? manifest = null;
        using (var zipStream = _fileSystem.OpenRead(backupFilePath))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
        {
            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry != null)
            {
                using var ms = manifestEntry.Open();
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(ms, JsonOptions, cancellationToken);
            }
        }

        if (manifest == null)
        {
            return new BackupValidationReport(false, "Manifest could not be read from backup archive.", null, false, false);
        }

        // 3. Version comparison: Check against current app migrations
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var allAppMigrations = context.Database.GetMigrations().ToList();
        var allAppMigrationsSet = new HashSet<string>(allAppMigrations, StringComparer.OrdinalIgnoreCase);

        var latestAppMigration = allAppMigrations.LastOrDefault() ?? string.Empty;
        var backupMigration = manifest.LatestMigrationId;

        if (!string.IsNullOrEmpty(backupMigration) && backupMigration != "None")
        {
            if (!allAppMigrationsSet.Contains(backupMigration))
            {
                // Backup contains a migration this version of DoctorRx does NOT have -> Made by a newer version!
                var msg = "This backup was created by a newer version of DoctorRx. Please update DoctorRx before restoring.";
                _logger.LogWarning("Restore rejected: {Message} (Backup migration: {Migration})", msg, backupMigration);
                return new BackupValidationReport(false, msg, manifest, false, true);
            }

            // If backup migration is older than latest available migration, it will need to be migrated forward
            bool requiresMigration = !string.Equals(backupMigration, latestAppMigration, StringComparison.OrdinalIgnoreCase);
            return new BackupValidationReport(true, null, manifest, requiresMigration, false);
        }

        return new BackupValidationReport(true, null, manifest, false, false);
    }

    public async Task<RestoreResult> RestoreFromBackupAsync(
        string backupFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting database restore from backup: {BackupPath}", backupFilePath);
        progress?.Report(0.05);

        // 1. Validate the backup file
        var validation = await ValidateBackupForRestoreAsync(backupFilePath, cancellationToken);
        if (!validation.CanRestore)
        {
            _logger.LogError("Restore aborted during validation: {Reason}", validation.Reason);
            return new RestoreResult(false, validation.Reason ?? "Validation failed.", null, false);
        }

        progress?.Report(0.15);

        // 2. Create mandatory pre-restore safety backup of current data
        _fileSystem.CreateDirectory(_appPaths.SafetyBackupsDirectory);
        var safetyTimestamp = _clock.UtcNow.ToString("yyyyMMddHHmmss");
        var safetyBackupResult = await _backupService.CreateBackupAsync(
            _appPaths.SafetyBackupsDirectory,
            null,
            cancellationToken);

        if (!safetyBackupResult.Success || safetyBackupResult.BackupFilePath == null)
        {
            _logger.LogError("Failed to create pre-restore safety backup. Aborting restore to prevent data loss.");
            return new RestoreResult(false, "Could not create safety backup before restore. Operation cancelled for safety.", null, false);
        }

        var safetyBackupPath = safetyBackupResult.BackupFilePath;
        _logger.LogInformation("Pre-restore safety backup safely created at {SafetyBackupPath}", safetyBackupPath);
        progress?.Report(0.35);

        string stagingRestoredDb = Path.Combine(_appPaths.DataDirectory, $"doctorrx.db.restore.{Guid.NewGuid():N}.tmp");
        string originalDbBackup = Path.Combine(_appPaths.DataDirectory, "doctorrx.db.swap.bak");

        try
        {
            // 3. Extract snapshot.db to staging file next to the database
            using (var zipStream = _fileSystem.OpenRead(backupFilePath))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                var dbEntry = archive.GetEntry("snapshot.db");
                if (dbEntry == null)
                {
                    throw new InvalidOperationException("Backup archive does not contain snapshot.db.");
                }

                using (var entryStream = dbEntry.Open())
                using (var targetFs = _fileSystem.CreateFile(stagingRestoredDb))
                {
                    await entryStream.CopyToAsync(targetFs, cancellationToken);
                }
            }

            progress?.Report(0.55);

            // 4. Verify the staging database before swapping
            var stagingConnStr = new SqliteConnectionStringBuilder
            {
                DataSource = stagingRestoredDb,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ConnectionString;

            await using (var conn = new SqliteConnection(stagingConnStr))
            {
                await conn.OpenAsync(cancellationToken);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                var check = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();
                if (!string.Equals(check, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Staging database integrity check failed: {check}");
                }
                await conn.CloseAsync();
            }

            progress?.Report(0.70);

            // 5. Close all active database connections & clear pools
            SqliteConnection.ClearAllPools();

            // Handle WAL and SHM files
            var walPath = _appPaths.DatabasePath + "-wal";
            var shmPath = _appPaths.DatabasePath + "-shm";
            _fileSystem.DeleteFile(walPath);
            _fileSystem.DeleteFile(shmPath);

            // 6. Atomic swap: Move current DB to swap.bak, move staging DB to live DB
            if (_fileSystem.FileExists(_appPaths.DatabasePath))
            {
                _fileSystem.MoveFile(_appPaths.DatabasePath, originalDbBackup, overwrite: true);
            }
            _fileSystem.MoveFile(stagingRestoredDb, _appPaths.DatabasePath, overwrite: true);

            progress?.Report(0.85);

            // 7. Restore settings JSON files from archive (excluding window-placement.json)
            RestoreSettingsFromArchive(backupFilePath);

            // 8. If backup was from an older version, migrate schema forward
            if (validation.RequiresMigration)
            {
                _logger.LogInformation("Backup is from an older schema version. Applying pending migrations forward...");
                await _databaseMigrator.MigrateDatabaseAsync(cancellationToken);
            }

            // 9. Post-restore verification
            SqliteConnection.ClearAllPools();
            var restoredConnStr = new SqliteConnectionStringBuilder
            {
                DataSource = _appPaths.DatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ConnectionString;

            await using (var conn = new SqliteConnection(restoredConnStr))
            {
                await conn.OpenAsync(cancellationToken);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                var postCheck = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();
                if (!string.Equals(postCheck, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Restored database failed post-restore integrity check: {postCheck}");
                }
                await conn.CloseAsync();
            }

            // Swap succeeded: clean up swap.bak
            _fileSystem.DeleteFile(originalDbBackup);

            progress?.Report(1.0);
            _logger.LogInformation("Restore completed successfully from {BackupPath}", backupFilePath);
            return new RestoreResult(true, null, safetyBackupPath, RolledBack: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed during swap or verification! Rolling back to pre-restore safety copy {SafetyBackupPath}...", safetyBackupPath);

            // ROLLBACK PIPELINE
            SqliteConnection.ClearAllPools();
            try
            {
                _fileSystem.DeleteFile(stagingRestoredDb);
                var walPath = _appPaths.DatabasePath + "-wal";
                var shmPath = _appPaths.DatabasePath + "-shm";
                _fileSystem.DeleteFile(walPath);
                _fileSystem.DeleteFile(shmPath);

                if (_fileSystem.FileExists(originalDbBackup))
                {
                    _fileSystem.MoveFile(originalDbBackup, _appPaths.DatabasePath, overwrite: true);
                }
                else if (_fileSystem.FileExists(safetyBackupPath))
                {
                    // Extract safety backup database over the current DB
                    using var zipStream = _fileSystem.OpenRead(safetyBackupPath);
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                    var dbEntry = archive.GetEntry("snapshot.db");
                    if (dbEntry != null)
                    {
                        using var entryStream = dbEntry.Open();
                        using var fs = _fileSystem.CreateFile(_appPaths.DatabasePath);
                        entryStream.CopyTo(fs);
                    }
                }

                _logger.LogWarning("Rollback completed successfully. Original database state restored.");
            }
            catch (Exception rollEx)
            {
                _logger.LogCritical(rollEx, "Critical error during rollback to safety backup!");
            }

            return new RestoreResult(false, $"Restore failed: {ex.Message}. Database was safely rolled back to original state.", safetyBackupPath, RolledBack: true);
        }
    }

    private void RestoreSettingsFromArchive(string backupFilePath)
    {
        try
        {
            using var zipStream = _fileSystem.OpenRead(backupFilePath);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("settings/", StringComparison.OrdinalIgnoreCase)))
            {
                var fileName = Path.GetFileName(entry.FullName);
                if (string.IsNullOrWhiteSpace(fileName)) continue;

                if (string.Equals(fileName, "window-placement.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Never overwrite local window placement
                }

                var targetPath = Path.Combine(_appPaths.BaseDirectory, fileName);
                using var entryStream = entry.Open();
                using var fs = _fileSystem.CreateFile(targetPath);
                entryStream.CopyTo(fs);
                _logger.LogInformation("Restored settings file {FileName}", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not restore auxiliary settings files from backup");
        }
    }
}
