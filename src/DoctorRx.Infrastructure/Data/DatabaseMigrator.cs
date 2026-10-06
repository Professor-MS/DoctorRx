using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Infrastructure.Data;

public class DatabaseMigrator : IDatabaseMigrator
{
    private readonly IDbContextFactory<DoctorRxDbContext> _contextFactory;
    private readonly IAppPaths _appPaths;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(
        IDbContextFactory<DoctorRxDbContext> contextFactory,
        IAppPaths appPaths,
        ILogger<DatabaseMigrator> logger)
    {
        _contextFactory = contextFactory;
        _appPaths = appPaths;
        _logger = logger;
    }

    public async Task MigrateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var dbFile = new FileInfo(_appPaths.DatabasePath);
        bool existsAndHasData = dbFile.Exists && dbFile.Length > 0;

        if (!existsAndHasData)
        {
            _logger.LogInformation("Fresh database detected at {DatabasePath}. Applying baseline migrations without pre-migration backup.", _appPaths.DatabasePath);
            await using var freshContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await freshContext.Database.MigrateAsync(cancellationToken);
            return;
        }

        // 1. Detect legacy EnsureCreated database (user tables exist, but no __EFMigrationsHistory)
        await DetectLegacyDatabaseAsync(cancellationToken);

        // 2. Check for pending migrations
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pendingMigrations.Count == 0)
        {
            _logger.LogInformation("Database is up-to-date. No pending migrations.");
            return;
        }

        _logger.LogInformation("Found {Count} pending migrations ({Migrations}). Creating pre-migration backup...",
            pendingMigrations.Count, string.Join(", ", pendingMigrations));

        // 3. Create pre-migration backup using SQLite Online Backup API
        Directory.CreateDirectory(_appPaths.BackupsDirectory);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupPath = Path.Combine(_appPaths.BackupsDirectory, $"pre-migration-{timestamp}.db");

        await CreateOnlineBackupAsync(context, backupPath, cancellationToken);
        _logger.LogInformation("Pre-migration backup safely saved to {BackupPath}", backupPath);

        // 4. Prune old pre-migration backups, retaining only the last 5
        PruneOldPreMigrationBackups(_appPaths.BackupsDirectory, maxBackupsToKeep: 5);

        // 5. Apply migrations; if migration fails, leave original file untouched / restored
        try
        {
            await context.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database migration failed! Reverting database file from pre-migration backup {BackupPath}...", backupPath);

            await context.Database.CloseConnectionAsync();
            SqliteConnection.ClearAllPools();

            // Restore from the pre-migration backup so original file is left untouched
            File.Copy(backupPath, _appPaths.DatabasePath, overwrite: true);
            _logger.LogWarning("Original database file restored from {BackupPath}.", backupPath);
            throw;
        }
    }

    private async Task DetectLegacyDatabaseAsync(CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _appPaths.DatabasePath,
            Mode = SqliteOpenMode.ReadOnly
        };

        await using var conn = new SqliteConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        long historyCount;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory';";
            historyCount = (long)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        long userTableCount;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name != '__EFMigrationsHistory';";
            userTableCount = (long)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        if (userTableCount > 0 && historyCount == 0)
        {
            _logger.LogError("Legacy database detected without __EFMigrationsHistory table at {DatabasePath}", _appPaths.DatabasePath);
            throw new InvalidOperationException(
                "Legacy unversioned database detected (created without EF Core migrations). " +
                "Please back up your existing database or migrate its schema before running DoctorRx.");
        }
    }

    private async Task CreateOnlineBackupAsync(DoctorRxDbContext context, string targetPath, CancellationToken cancellationToken)
    {
        var rawConn = context.Database.GetDbConnection();
        if (rawConn is SqliteConnection sourceConn)
        {
            if (sourceConn.State != System.Data.ConnectionState.Open)
            {
                await sourceConn.OpenAsync(cancellationToken);
            }

            var destConnStr = new SqliteConnectionStringBuilder { DataSource = targetPath }.ConnectionString;
            await using var destConn = new SqliteConnection(destConnStr);
            await destConn.OpenAsync(cancellationToken);

            sourceConn.BackupDatabase(destConn);
        }
        else
        {
            File.Copy(_appPaths.DatabasePath, targetPath, overwrite: true);
        }
    }

    private void PruneOldPreMigrationBackups(string backupsDirectory, int maxBackupsToKeep)
    {
        try
        {
            if (!Directory.Exists(backupsDirectory)) return;

            var backupFiles = Directory.GetFiles(backupsDirectory, "pre-migration-*.db")
                .Select(path => new FileInfo(path))
                .OrderByDescending(fi => fi.CreationTimeUtc)
                .ToList();

            if (backupFiles.Count > maxBackupsToKeep)
            {
                var filesToDelete = backupFiles.Skip(maxBackupsToKeep);
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        file.Delete();
                        _logger.LogInformation("Pruned old pre-migration backup {FilePath}", file.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete old backup {FilePath}", file.FullName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to prune old pre-migration backups in {Directory}", backupsDirectory);
        }
    }
}
