using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Infrastructure.Services;

public class DatabaseHealthService : IDatabaseHealthService
{
    private readonly IDbContextFactory<DoctorRxDbContext> _contextFactory;
    private readonly IAppPaths _appPaths;
    private readonly IFileSystem _fileSystem;
    private readonly IClock _clock;
    private readonly ISearchIndexRepairService _searchIndexRepairService;
    private readonly ILogger<DatabaseHealthService> _logger;

    public DatabaseHealthService(
        IDbContextFactory<DoctorRxDbContext> contextFactory,
        IAppPaths appPaths,
        IFileSystem fileSystem,
        IClock clock,
        ISearchIndexRepairService searchIndexRepairService,
        ILogger<DatabaseHealthService> logger)
    {
        _contextFactory = contextFactory;
        _appPaths = appPaths;
        _fileSystem = fileSystem;
        _clock = clock;
        _searchIndexRepairService = searchIndexRepairService;
        _logger = logger;
    }

    public bool IsNetworkOrUncPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // 1. Detect UNC path (\\server\share or //server/share)
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        // 2. Detect mapped network drive
        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root))
            {
                var driveInfo = new DriveInfo(root);
                if (driveInfo.DriveType == DriveType.Network)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve DriveInfo for path: {Path}", path);
        }

        return false;
    }

    public long GetAvailableFreeSpaceBytes()
    {
        return _fileSystem.GetAvailableFreeSpace(_appPaths.DataDirectory);
    }

    public async Task<DatabaseHealthReport> RunQuickCheckAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.FileExists(_appPaths.DatabasePath))
        {
            return new DatabaseHealthReport(
                IsHealthy: true,
                QuickCheckPassed: true,
                IntegrityCheckPassed: true,
                ErrorDetails: null,
                CorruptQuarantinePath: null,
                SearchIndexHealthy: true,
                DatabaseSizeBytes: 0);
        }

        long sizeBytes = _fileSystem.GetFileSize(_appPaths.DatabasePath);
        if (sizeBytes == 0)
        {
            return new DatabaseHealthReport(
                IsHealthy: true,
                QuickCheckPassed: true,
                IntegrityCheckPassed: true,
                ErrorDetails: null,
                CorruptQuarantinePath: null,
                SearchIndexHealthy: true,
                DatabaseSizeBytes: 0);
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _appPaths.DatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            await using var conn = new SqliteConnection(builder.ConnectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check;";

            var result = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();

            bool passed = string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
            if (!passed)
            {
                _logger.LogError("Database PRAGMA quick_check reported failure: {Result}", result);
            }
            else
            {
                try
                {
                    await using var checkCmd = conn.CreateCommand();
                    checkCmd.CommandText = "SELECT COUNT(1) FROM Prescriptions WHERE Status = 1 AND IsSealed = 0;";
                    var unsealedCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken) ?? 0);
                    if (unsealedCount > 0)
                    {
                        _logger.LogWarning("Database health check: Found {Count} finalized prescriptions with IsSealed = 0", unsealedCount);
                    }
                }
                catch
                {
                    // Ignore if IsSealed column is not yet present
                }
            }
            await conn.CloseAsync();

            return new DatabaseHealthReport(
                IsHealthy: passed,
                QuickCheckPassed: passed,
                IntegrityCheckPassed: passed,
                ErrorDetails: passed ? null : result,
                CorruptQuarantinePath: null,
                SearchIndexHealthy: true,
                DatabaseSizeBytes: sizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while executing PRAGMA quick_check");
            return new DatabaseHealthReport(
                IsHealthy: false,
                QuickCheckPassed: false,
                IntegrityCheckPassed: false,
                ErrorDetails: ex.Message,
                CorruptQuarantinePath: null,
                SearchIndexHealthy: false,
                DatabaseSizeBytes: sizeBytes);
        }
    }

    public async Task<DatabaseHealthReport> RunFullIntegrityCheckAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.FileExists(_appPaths.DatabasePath))
        {
            return new DatabaseHealthReport(
                IsHealthy: true,
                QuickCheckPassed: true,
                IntegrityCheckPassed: true,
                ErrorDetails: null,
                CorruptQuarantinePath: null,
                SearchIndexHealthy: true,
                DatabaseSizeBytes: 0);
        }

        long sizeBytes = _fileSystem.GetFileSize(_appPaths.DatabasePath);
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _appPaths.DatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            await using var conn = new SqliteConnection(builder.ConnectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";

            var result = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();
            await conn.CloseAsync();

            bool passed = string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);

            // Also verify search index tokens
            bool searchIndexHealthy = true;
            try
            {
                await _searchIndexRepairService.RepairIndexAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search index verification failed during integrity check");
                searchIndexHealthy = false;
            }

            return new DatabaseHealthReport(
                IsHealthy: passed && searchIndexHealthy,
                QuickCheckPassed: passed,
                IntegrityCheckPassed: passed,
                ErrorDetails: passed ? null : result,
                CorruptQuarantinePath: null,
                SearchIndexHealthy: searchIndexHealthy,
                DatabaseSizeBytes: sizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while executing PRAGMA integrity_check");
            return new DatabaseHealthReport(
                IsHealthy: false,
                QuickCheckPassed: false,
                IntegrityCheckPassed: false,
                ErrorDetails: ex.Message,
                CorruptQuarantinePath: null,
                SearchIndexHealthy: false,
                DatabaseSizeBytes: sizeBytes);
        }
    }

    public Task<string?> QuarantineCorruptDatabaseAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.FileExists(_appPaths.DatabasePath))
        {
            return Task.FromResult<string?>(null);
        }

        SqliteConnection.ClearAllPools();

        var timestamp = _clock.UtcNow.ToString("yyyyMMddHHmmss");
        var quarantineDbName = $"doctorrx.corrupt-{timestamp}.db";
        var quarantineDbPath = Path.Combine(_appPaths.DataDirectory, quarantineDbName);

        try
        {
            _fileSystem.MoveFile(_appPaths.DatabasePath, quarantineDbPath, overwrite: true);

            // Also quarantine associated WAL and SHM files if present
            var wal = _appPaths.DatabasePath + "-wal";
            var shm = _appPaths.DatabasePath + "-shm";
            if (_fileSystem.FileExists(wal))
            {
                _fileSystem.MoveFile(wal, quarantineDbPath + "-wal", overwrite: true);
            }
            if (_fileSystem.FileExists(shm))
            {
                _fileSystem.MoveFile(shm, quarantineDbPath + "-shm", overwrite: true);
            }

            _logger.LogCritical("Corrupt database quarantined to {QuarantinePath}", quarantineDbPath);
            return Task.FromResult<string?>(quarantineDbPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to quarantine corrupt database file to {QuarantinePath}", quarantineDbPath);
            return Task.FromResult<string?>(null);
        }
    }

    public async Task CheckpointWalAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.FileExists(_appPaths.DatabasePath))
        {
            return;
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _appPaths.DatabasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            };

            await using var conn = new SqliteConnection(builder.ConnectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            await conn.CloseAsync();

            _logger.LogInformation("Database WAL checkpoint(TRUNCATE) successfully completed on shutdown.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not execute PRAGMA wal_checkpoint(TRUNCATE)");
        }
    }
}
