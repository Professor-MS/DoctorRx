using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Infrastructure.Data;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDatabaseMigrator _databaseMigrator;
    private readonly IDemoDataSeeder _demoDataSeeder;
    private readonly ISearchIndexRepairService _searchIndexRepairService;
    private readonly IDatabaseHealthService _databaseHealthService;
    private readonly IAppPaths _appPaths;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDatabaseMigrator databaseMigrator,
        IDemoDataSeeder demoDataSeeder,
        ISearchIndexRepairService searchIndexRepairService,
        IDatabaseHealthService databaseHealthService,
        IAppPaths appPaths,
        ILogger<DatabaseInitializer> logger)
    {
        _databaseMigrator = databaseMigrator;
        _demoDataSeeder = demoDataSeeder;
        _searchIndexRepairService = searchIndexRepairService;
        _databaseHealthService = databaseHealthService;
        _appPaths = appPaths;
        _logger = logger;
    }

    public static bool ShouldSeedDemoData(string[]? args = null)
    {
        var env = Environment.GetEnvironmentVariable("DOCTORRX_DEMO");
        if (string.Equals(env, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var cmdArgs = args ?? Environment.GetCommandLineArgs();
        return cmdArgs != null && cmdArgs.Any(a => string.Equals(a, "--demo-data", StringComparison.OrdinalIgnoreCase));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 1. Guard against Network Shares / UNC paths
        if (_databaseHealthService.IsNetworkOrUncPath(_appPaths.DatabasePath))
        {
            var msg = $"DoctorRx database path is located on a network share or network drive: {_appPaths.DatabasePath}";
            var guidance = "DoctorRx cannot run on network shares or mapped network drives because SQLite locking over network filesystems causes silent database corruption. Please move your DoctorRx data directory to a local hard drive (such as C:\\Users\\...).";
            _logger.LogCritical("Startup aborted: {Message}", msg);
            throw new DoctorRxStartupException(StartupFailureReason.NetworkDriveDetected, msg, guidance);
        }

        // 2. Guard against Low Disk Space (< 50MB)
        var freeSpace = _databaseHealthService.GetAvailableFreeSpaceBytes();
        if (freeSpace < 50L * 1024 * 1024)
        {
            var freeMb = freeSpace / (1024 * 1024);
            var msg = $"Low disk space detected: {freeMb} MB free on database drive.";
            var guidance = "DoctorRx requires at least 50 MB of free disk space to operate safely and prevent record corruption. Please free up space on your disk and restart the application.";
            _logger.LogCritical("Startup aborted: {Message}", msg);
            throw new DoctorRxStartupException(StartupFailureReason.InsufficientDiskSpace, msg, guidance);
        }

        // 3. Pre-migration Database Health Quick Check
        var healthReport = await _databaseHealthService.RunQuickCheckAsync(cancellationToken);
        if (!healthReport.IsHealthy)
        {
            _logger.LogError("Corrupt database detected during startup quick check: {ErrorDetails}", healthReport.ErrorDetails);
            var quarantinePath = await _databaseHealthService.QuarantineCorruptDatabaseAsync(cancellationToken);

            var msg = $"Database integrity check failed: {healthReport.ErrorDetails}";
            var guidance = $"DoctorRx detected corruption in your database file. To prevent data loss, the file was safely quarantined to: {quarantinePath ?? "doctorrx.corrupt"}. A recent backup can be restored from the Settings screen or your Backups folder.";
            throw new DoctorRxStartupException(StartupFailureReason.DatabaseCorrupt, msg, guidance);
        }

        // 4. Apply Schema Migrations
        try
        {
            _logger.LogInformation("Applying SQLite database migrations...");
            await _databaseMigrator.MigrateDatabaseAsync(cancellationToken);

            if (ShouldSeedDemoData())
            {
                _logger.LogInformation("Demo data flag detected (DOCTORRX_DEMO=1 or --demo-data). Seeding demo entities...");
                await _demoDataSeeder.SeedAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("Demo data seeding skipped (clean production mode).");
            }

            _logger.LogInformation("Verifying search index integrity...");
            await _searchIndexRepairService.RepairIndexAsync(cancellationToken);
        }
        catch (DoctorRxStartupException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied to database directory.");
            throw new DoctorRxStartupException(
                StartupFailureReason.AccessDenied,
                ex.Message,
                $"DoctorRx does not have permission to access '{_appPaths.DataDirectory}'. Please ensure your Windows account has full read/write permissions to this folder.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initializing the SQLite database.");
            throw;
        }
    }
}
