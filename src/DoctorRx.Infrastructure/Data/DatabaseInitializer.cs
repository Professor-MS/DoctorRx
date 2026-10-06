using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Infrastructure.Data;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDatabaseMigrator _databaseMigrator;
    private readonly IDemoDataSeeder _demoDataSeeder;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDatabaseMigrator databaseMigrator,
        IDemoDataSeeder demoDataSeeder,
        ILogger<DatabaseInitializer> logger)
    {
        _databaseMigrator = databaseMigrator;
        _demoDataSeeder = demoDataSeeder;
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initializing the SQLite database.");
            throw;
        }
    }
}
