using System;
using System.Collections.Generic;
using System.Data;
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

public class BackupService : IBackupService
{
    private const int CurrentFormatVersion = 1;
    private const string CurrentAppVersion = "1.0.0";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IDbContextFactory<DoctorRxDbContext> _contextFactory;
    private readonly IAppPaths _appPaths;
    private readonly IFileSystem _fileSystem;
    private readonly IClock _clock;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IDbContextFactory<DoctorRxDbContext> contextFactory,
        IAppPaths appPaths,
        IFileSystem fileSystem,
        IClock clock,
        ILogger<BackupService> logger)
    {
        _contextFactory = contextFactory;
        _appPaths = appPaths;
        _fileSystem = fileSystem;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BackupResult> CreateBackupAsync(
        string targetDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting database backup process...");
        progress?.Report(0.05);

        if (!_fileSystem.FileExists(_appPaths.DatabasePath))
        {
            _logger.LogError("Database file not found at {DatabasePath}", _appPaths.DatabasePath);
            return new BackupResult(false, null, "Database file does not exist.", null, false);
        }

        long dbFileSize = _fileSystem.GetFileSize(_appPaths.DatabasePath);
        long requiredSpace = Math.Max(dbFileSize * 2, 1024 * 1024 * 2); // At least 2x db size or 2MB minimum

        _fileSystem.CreateDirectory(targetDirectory);
        long freeSpace = _fileSystem.GetAvailableFreeSpace(targetDirectory);

        if (freeSpace < requiredSpace)
        {
            long reqMb = requiredSpace / (1024 * 1024);
            long freeMb = freeSpace / (1024 * 1024);
            var msg = $"Not enough free disk space for backup. Available: {freeMb} MB, Required: {reqMb} MB.";
            _logger.LogWarning("Backup rejected: {Message}", msg);
            return new BackupResult(false, null, msg, null, false);
        }

        var timestamp = _clock.UtcNow.ToString("yyyy-MM-dd-HHmm");
        var baseFileName = $"DoctorRx-Backup-{timestamp}";
        var finalFilePath = Path.Combine(targetDirectory, $"{baseFileName}.drxbackup");
        var tmpFilePath = Path.Combine(targetDirectory, $"{baseFileName}.drxbackup.tmp");

        // Discard any leftover tmp file from previous interrupted run
        _fileSystem.DeleteFile(tmpFilePath);

        string tempSnapshotDb = Path.Combine(Path.GetTempPath(), $"drx_snap_{Guid.NewGuid():N}.db");

        try
        {
            progress?.Report(0.15);

            // 1. Take online snapshot using SQLite Online Backup API into temporary file
            await CreateOnlineSnapshotAsync(tempSnapshotDb, cancellationToken);
            progress?.Report(0.40);

            // 2. Open snapshot read-only to calculate hash, table counts, latest migration
            string dbHash;
            using (var stream = File.Open(tempSnapshotDb, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using var sha256 = SHA256.Create();
                var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
                dbHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            var (migrationId, tableCounts) = await InspectSnapshotMetadataAsync(tempSnapshotDb, cancellationToken);
            var manifest = new BackupManifest(
                FormatVersion: CurrentFormatVersion,
                AppVersion: CurrentAppVersion,
                LatestMigrationId: migrationId,
                CreatedAtUtc: _clock.UtcNow,
                TableRowCounts: tableCounts,
                DatabaseSha256: dbHash,
                DatabaseSizeBytes: new FileInfo(tempSnapshotDb).Length
            );

            progress?.Report(0.60);

            // 3. Assemble .drxbackup ZIP archive into *.tmp file
            using (var zipStream = _fileSystem.CreateFile(tmpFilePath))
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
                {
                    // Add database snapshot
                    var dbEntry = archive.CreateEntry("snapshot.db", CompressionLevel.Optimal);
                    using (var entryStream = dbEntry.Open())
                    using (var fs = File.OpenRead(tempSnapshotDb))
                    {
                        await fs.CopyToAsync(entryStream, cancellationToken);
                    }

                    // Add manifest
                    var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                    using (var entryStream = manifestEntry.Open())
                    {
                        await JsonSerializer.SerializeAsync(entryStream, manifest, JsonOptions, cancellationToken);
                    }

                    // Add settings JSON files (excluding window-placement.json)
                    AddSettingsFilesToArchive(archive);
                }
            }

            progress?.Report(0.80);

            // 4. Verify the backup immediately
            var verification = await VerifyBackupInternalAsync(tmpFilePath, cancellationToken);
            if (!verification.IsValid)
            {
                _fileSystem.DeleteFile(tmpFilePath);
                _logger.LogError("Backup immediate verification failed: {Error}", verification.ErrorMessage);
                return new BackupResult(false, null, $"Backup verification failed: {verification.ErrorMessage}", manifest, false);
            }

            // 5. Atomic rename from *.tmp to final .drxbackup
            _fileSystem.MoveFile(tmpFilePath, finalFilePath, overwrite: true);
            progress?.Report(1.0);

            _logger.LogInformation("Backup created and verified successfully at {FilePath}. Tables counted: {Count}",
                finalFilePath, tableCounts.Count);

            return new BackupResult(true, finalFilePath, null, manifest, true);
        }
        catch (Exception ex)
        {
            _fileSystem.DeleteFile(tmpFilePath);
            _logger.LogError(ex, "Unexpected error occurred during backup creation");
            return new BackupResult(false, null, $"Backup failed: {ex.Message} ({ex.GetType().Name})", null, false);
        }
        finally
        {
            try
            {
                if (File.Exists(tempSnapshotDb))
                {
                    File.Delete(tempSnapshotDb);
                }
            }
            catch
            {
                // Best-effort cleanup of temp snapshot
            }
        }
    }

    public async Task<BackupVerificationResult> VerifyBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.FileExists(backupFilePath))
        {
            return new BackupVerificationResult(false, "Failed", "Backup file does not exist.", null, null, false, false);
        }

        return await VerifyBackupInternalAsync(backupFilePath, cancellationToken);
    }

    public Task<IReadOnlyList<BackupMetadataDto>> GetAvailableBackupsAsync(string directory, CancellationToken cancellationToken = default)
    {
        var result = new List<BackupMetadataDto>();
        if (!_fileSystem.DirectoryExists(directory))
        {
            return Task.FromResult<IReadOnlyList<BackupMetadataDto>>(result);
        }

        var files = _fileSystem.GetFiles(directory, "*.drxbackup");
        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                BackupManifest? manifest = null;
                bool isVerified = false;

                using (var zipStream = _fileSystem.OpenRead(file))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    var manifestEntry = archive.GetEntry("manifest.json");
                    if (manifestEntry != null)
                    {
                        using var ms = manifestEntry.Open();
                        manifest = JsonSerializer.Deserialize<BackupManifest>(ms);
                        isVerified = true;
                    }
                }

                result.Add(new BackupMetadataDto(
                    FilePath: file,
                    FileName: Path.GetFileName(file),
                    FileSizeBytes: fileInfo.Length,
                    CreatedAtUtc: manifest?.CreatedAtUtc ?? _fileSystem.GetCreationTimeUtc(file),
                    IsVerified: isVerified,
                    Manifest: manifest
                ));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read backup file metadata for {FilePath}", file);
                result.Add(new BackupMetadataDto(
                    FilePath: file,
                    FileName: Path.GetFileName(file),
                    FileSizeBytes: _fileSystem.GetFileSize(file),
                    CreatedAtUtc: _fileSystem.GetCreationTimeUtc(file),
                    IsVerified: false,
                    Manifest: null
                ));
            }
        }

        var sorted = result.OrderByDescending(b => b.CreatedAtUtc).ToList();
        return Task.FromResult<IReadOnlyList<BackupMetadataDto>>(sorted);
    }

    private async Task CreateOnlineSnapshotAsync(string targetPath, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var rawConn = context.Database.GetDbConnection();

        if (rawConn is SqliteConnection sourceConn)
        {
            if (sourceConn.State != ConnectionState.Open)
            {
                await sourceConn.OpenAsync(cancellationToken);
            }

            var destConnStr = new SqliteConnectionStringBuilder
            {
                DataSource = targetPath,
                Pooling = false
            }.ConnectionString;
            await using var destConn = new SqliteConnection(destConnStr);
            await destConn.OpenAsync(cancellationToken);

            sourceConn.BackupDatabase(destConn);
            await destConn.CloseAsync();
        }
        else
        {
            throw new InvalidOperationException("Active database connection is not a SQLite connection.");
        }
    }

    private async Task<(string MigrationId, Dictionary<string, int> Counts)> InspectSnapshotMetadataAsync(
        string snapshotPath,
        CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = snapshotPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };

        await using var conn = new SqliteConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        string latestMigration = "None";
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1;";
            try
            {
                var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
                if (scalar != null) latestMigration = scalar.ToString() ?? "None";
            }
            catch
            {
                // No migration table
            }
        }

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var mainTables = new[] { "Patients", "Prescriptions", "PrescriptionMedicines", "Medicines", "Doctors", "Drafts" };

        foreach (var table in mainTables)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
            try
            {
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0);
                counts[table] = count;
            }
            catch
            {
                counts[table] = 0;
            }
        }

        return (latestMigration, counts);
    }

    private void AddSettingsFilesToArchive(ZipArchive archive)
    {
        try
        {
            if (!_fileSystem.DirectoryExists(_appPaths.BaseDirectory)) return;

            var jsonFiles = _fileSystem.GetFiles(_appPaths.BaseDirectory, "*.json");
            foreach (var jsonFile in jsonFiles)
            {
                var fileName = Path.GetFileName(jsonFile);
                if (string.Equals(fileName, "window-placement.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Window placement is strictly excluded from backups
                }

                var entry = archive.CreateEntry($"settings/{fileName}", CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var fileStream = _fileSystem.OpenRead(jsonFile);
                fileStream.CopyTo(entryStream);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not bundle settings files into backup archive");
        }
    }

    private async Task<BackupVerificationResult> VerifyBackupInternalAsync(string archivePath, CancellationToken cancellationToken)
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"drx_verify_{Guid.NewGuid():N}.db");
        try
        {
            BackupManifest? manifest = null;

            // 1. Extract snapshot.db and manifest.json from archive
            using (var zipStream = _fileSystem.OpenRead(archivePath))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry == null)
                {
                    return new BackupVerificationResult(false, "Failed", "Archive does not contain manifest.json.", null, null, false, false);
                }

                using (var ms = manifestEntry.Open())
                {
                    manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(ms, cancellationToken: cancellationToken);
                }

                var dbEntry = archive.GetEntry("snapshot.db");
                if (dbEntry == null)
                {
                    return new BackupVerificationResult(false, "Failed", "Archive does not contain snapshot.db.", null, null, false, false);
                }

                using (var entryStream = dbEntry.Open())
                using (var targetFs = File.Create(tempDbPath))
                {
                    await entryStream.CopyToAsync(targetFs, cancellationToken);
                }
            }

            if (manifest == null)
            {
                return new BackupVerificationResult(false, "Failed", "Manifest could not be parsed.", null, null, false, false);
            }

            // 2. Validate SHA-256
            string computedHash;
            using (var stream = File.OpenRead(tempDbPath))
            {
                using var sha256 = SHA256.Create();
                var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
                computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            bool hashMatched = string.Equals(computedHash, manifest.DatabaseSha256, StringComparison.OrdinalIgnoreCase);
            if (!hashMatched)
            {
                return new BackupVerificationResult(false, "Failed", "Database checksum does not match manifest.", null, null, false, false);
            }

            // 3. Open temporary database and run PRAGMA checks
            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = tempDbPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ConnectionString;

            await using var conn = new SqliteConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            string integrityResult = "ok";
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA integrity_check;";
                integrityResult = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString() ?? "error";
            }

            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return new BackupVerificationResult(false, "Failed", $"PRAGMA integrity_check failed: {integrityResult}", integrityResult, null, true, false);
            }

            string fkCheckResult = "ok";
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_key_check;";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    fkCheckResult = "Foreign key violations detected";
                    return new BackupVerificationResult(false, "Failed", fkCheckResult, integrityResult, fkCheckResult, true, false);
                }
            }

            // 4. Verify table counts match
            bool countsMatched = true;
            foreach (var kvp in manifest.TableRowCounts)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM {kvp.Key};";
                try
                {
                    var actualCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0);
                    if (actualCount != kvp.Value)
                    {
                        countsMatched = false;
                        return new BackupVerificationResult(false, "Failed",
                            $"Row count mismatch on table {kvp.Key}: expected {kvp.Value}, found {actualCount}",
                            integrityResult, fkCheckResult, true, false);
                    }
                }
                catch
                {
                    countsMatched = false;
                    return new BackupVerificationResult(false, "Failed", $"Table {kvp.Key} not found in database.", integrityResult, fkCheckResult, true, false);
                }
            }

            return new BackupVerificationResult(true, "Verified", null, integrityResult, fkCheckResult, true, countsMatched);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup verification encountered an exception");
            return new BackupVerificationResult(false, "Failed", $"Verification error: {ex.Message}", null, null, false, false);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (File.Exists(tempDbPath))
                {
                    File.Delete(tempDbPath);
                }
            }
            catch
            {
                // Best effort
            }
        }
    }
}
