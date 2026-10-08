using System;
using System.IO;
using System.Threading.Tasks;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DoctorRx.Tests;

public class SqliteConfigurationTests : IDisposable
{
    private readonly string _tempDirectory;

    public SqliteConfigurationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DoctorRx_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void AppPaths_CreatesExpectedFoldersUnderBaseDirectory()
    {
        // Arrange & Act
        var appPaths = new AppPaths(_tempDirectory);

        // Assert
        Assert.True(Directory.Exists(appPaths.DataDirectory));
        Assert.True(Directory.Exists(appPaths.BackupsDirectory));
        Assert.True(Directory.Exists(appPaths.DraftsDirectory));
        Assert.True(Directory.Exists(appPaths.LogsDirectory));
        Assert.True(Directory.Exists(appPaths.AssetsDirectory));
        Assert.Equal(Path.Combine(appPaths.DataDirectory, "doctorrx.db"), appPaths.DatabasePath);
    }

    [Fact]
    public async Task RealSqliteConnection_HasWalAndForeignKeysAndFullSyncConfigured()
    {
        // Arrange
        var appPaths = new AppPaths(_tempDirectory);
        var connStrBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = appPaths.DatabasePath
        };
        var interceptor = new SqlitePragmaInterceptor();

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(connStrBuilder.ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new DoctorRxDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync();
        }

        // Act & Assert 1: journal_mode = WAL
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode;";
            var result = (string?)await cmd.ExecuteScalarAsync();
            Assert.Equal("wal", result?.ToLowerInvariant());
        }

        // Act & Assert 2: foreign_keys = 1
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys;";
            var result = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(1, result);
        }

        // Act & Assert 3: busy_timeout = 5000
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA busy_timeout;";
            var result = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(5000, result);
        }

        // Act & Assert 4: synchronous = 2 (FULL)
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA synchronous;";
            var result = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(2, result);
        }

        await connection.CloseAsync();
        SqliteConnection.ClearAllPools();
    }
}
