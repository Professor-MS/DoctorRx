using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class RestoreEngineTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly PhysicalFileSystem _fileSystem;
    private readonly TestDbContextFactory _factory;
    private readonly IClock _clock;
    private readonly BackupService _backupService;
    private readonly DatabaseMigrator _migrator;
    private readonly RestoreService _restoreService;

    public RestoreEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_RestoreTests_" + Guid.NewGuid().ToString("N"));
        _appPaths = new TestAppPaths(_testDir);
        _fileSystem = new PhysicalFileSystem();
        _clock = new SystemClock();

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _appPaths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(builder.ConnectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _factory = new TestDbContextFactory(options);
        _backupService = new BackupService(_factory, _appPaths, _fileSystem, _clock, NullLogger<BackupService>.Instance);
        _migrator = new DatabaseMigrator(_factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        _restoreService = new RestoreService(
            _factory,
            _appPaths,
            _fileSystem,
            _backupService,
            _migrator,
            _clock,
            NullLogger<RestoreService>.Instance);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private async Task SeedInitialDatabaseAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();

        var doctor = new Doctor
        {
            Name = "Dr. Asim Munir",
            Qualification = "MBBS, MD",
            RegistrationNumber = "PMC-99887",
            Specialization = "Neurologist",
            ClinicName = "Neuro Health Center",
            IsActive = true
        };
        context.Doctors.Add(doctor);

        var patient = new Patient
        {
            Name = "Original Patient",
            NormalizedName = "original patient",
            RecordNumber = "MR-00500",
            Gender = Gender.Female,
            Phone = "03001234567",
            PhoneDigits = "03001234567"
        };
        context.Patients.Add(patient);

        var medicine = new Medicine
        {
            Name = "Panadol 500mg",
            NormalizedName = "panadol 500mg",
            Form = "Tablet",
            Strength = "500mg"
        };
        context.Medicines.Add(medicine);

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task RestoreFromBackup_SuccessfullyRestoresDataAndSettings()
    {
        // 1. Arrange: Seed DB and write settings
        await SeedInitialDatabaseAsync();

        var settingsPath = Path.Combine(_appPaths.BaseDirectory, "print-settings.json");
        Directory.CreateDirectory(_appPaths.BaseDirectory);
        await File.WriteAllTextAsync(settingsPath, "{\"PrinterName\":\"ClinicPrinterA\"}");

        // Create backup
        var backupResult = await _backupService.CreateBackupAsync(
            Path.Combine(_testDir, "Backups"),
            null,
            default);
        Assert.True(backupResult.Success);
        Assert.NotNull(backupResult.BackupFilePath);

        // Mutate current DB and settings
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var p = await context.Patients.FirstAsync();
            p.Name = "Altered Patient Name";
            var newPatient = new Patient
            {
                Name = "Mutated Patient",
                NormalizedName = "mutated patient",
                RecordNumber = "MR-99999",
                Gender = Gender.Male
            };
            context.Patients.Add(newPatient);
            await context.SaveChangesAsync();
        }
        await File.WriteAllTextAsync(settingsPath, "{\"PrinterName\":\"ModifiedPrinterB\"}");

        // 2. Act: Validate and Restore
        var validation = await _restoreService.ValidateBackupForRestoreAsync(backupResult.BackupFilePath);
        Assert.True(validation.CanRestore);
        Assert.False(validation.IsNewerVersion);

        var restoreResult = await _restoreService.RestoreFromBackupAsync(backupResult.BackupFilePath);

        // 3. Assert
        Assert.True(restoreResult.Success);
        Assert.False(restoreResult.RolledBack);
        Assert.NotNull(restoreResult.SafetyBackupPath);
        Assert.True(File.Exists(restoreResult.SafetyBackupPath));

        // Verify database contents reverted to original
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var patients = await context.Patients.ToListAsync();
            Assert.Single(patients);
            Assert.Equal("Original Patient", patients[0].Name);

            var doctors = await context.Doctors.ToListAsync();
            Assert.Single(doctors);
            Assert.Equal("Dr. Asim Munir", doctors[0].Name);

            var medicines = await context.Medicines.ToListAsync();
            Assert.Single(medicines);
            Assert.Equal("Panadol 500mg", medicines[0].Name);
        }

        // Verify settings reverted
        var restoredSettings = await File.ReadAllTextAsync(settingsPath);
        Assert.Contains("ClinicPrinterA", restoredSettings);
    }

    [Fact]
    public async Task ValidateBackup_RejectsNewerVersionBackup()
    {
        await SeedInitialDatabaseAsync();
        var backupResult = await _backupService.CreateBackupAsync(
            Path.Combine(_testDir, "Backups"),
            null,
            default);
        Assert.True(backupResult.Success);

        // Tamper with manifest inside archive to simulate a newer migration
        var modifiedBackupPath = Path.Combine(_testDir, "newer_version.drxbackup");
        File.Copy(backupResult.BackupFilePath!, modifiedBackupPath);

        using (var archive = ZipFile.Open(modifiedBackupPath, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("manifest.json");
            entry?.Delete();

            var newEntry = archive.CreateEntry("manifest.json");
            using var writer = new StreamWriter(newEntry.Open());
            var manifest = backupResult.Manifest! with { LatestMigrationId = "99991231_FutureMigration" };
            writer.Write(JsonSerializer.Serialize(manifest));
        }

        var validation = await _restoreService.ValidateBackupForRestoreAsync(modifiedBackupPath);
        Assert.False(validation.CanRestore);
        Assert.True(validation.IsNewerVersion);
        Assert.Contains("newer version", validation.Reason, StringComparison.OrdinalIgnoreCase);

        // Restore should abort without making changes
        var restoreResult = await _restoreService.RestoreFromBackupAsync(modifiedBackupPath);
        Assert.False(restoreResult.Success);
        Assert.Contains("newer version", restoreResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateBackup_FlagsRequiresMigrationForOlderVersion()
    {
        await SeedInitialDatabaseAsync();
        var backupResult = await _backupService.CreateBackupAsync(
            Path.Combine(_testDir, "Backups"),
            null,
            default);
        Assert.True(backupResult.Success);

        // Get the list of migrations
        await using var context = await _factory.CreateDbContextAsync();
        var allMigrations = context.Database.GetMigrations().ToList();
        Assert.True(allMigrations.Count > 1);

        // Tamper with manifest to reference the first (older) migration
        var olderBackupPath = Path.Combine(_testDir, "older_version.drxbackup");
        File.Copy(backupResult.BackupFilePath!, olderBackupPath);

        using (var archive = ZipFile.Open(olderBackupPath, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("manifest.json");
            entry?.Delete();

            var newEntry = archive.CreateEntry("manifest.json");
            using var writer = new StreamWriter(newEntry.Open());
            var manifest = backupResult.Manifest! with { LatestMigrationId = allMigrations.First() };
            writer.Write(JsonSerializer.Serialize(manifest));
        }

        var validation = await _restoreService.ValidateBackupForRestoreAsync(olderBackupPath);
        Assert.True(validation.CanRestore);
        Assert.False(validation.IsNewerVersion);
        Assert.True(validation.RequiresMigration);
    }

    [Fact]
    public async Task RestoreFromBackup_CleansUpWalAndShmFiles()
    {
        await SeedInitialDatabaseAsync();
        var backupResult = await _backupService.CreateBackupAsync(
            Path.Combine(_testDir, "Backups"),
            null,
            default);
        Assert.True(backupResult.Success);

        // Simulate orphaned WAL and SHM files
        var walPath = _appPaths.DatabasePath + "-wal";
        var shmPath = _appPaths.DatabasePath + "-shm";
        await File.WriteAllTextAsync(walPath, "dummy wal content");
        await File.WriteAllTextAsync(shmPath, "dummy shm content");

        Assert.True(File.Exists(walPath));
        Assert.True(File.Exists(shmPath));

        var restoreResult = await _restoreService.RestoreFromBackupAsync(backupResult.BackupFilePath!);
        Assert.True(restoreResult.Success);

        // Verify old dummy WAL and SHM contents were purged
        if (File.Exists(walPath))
        {
            var currentWalContent = await File.ReadAllTextAsync(walPath);
            Assert.DoesNotContain("dummy wal content", currentWalContent);
        }

        if (File.Exists(shmPath))
        {
            var currentShmContent = await File.ReadAllTextAsync(shmPath);
            Assert.DoesNotContain("dummy shm content", currentShmContent);
        }
    }

    [Fact]
    public async Task Restore_DoesNotOverwriteWindowPlacement()
    {
        await SeedInitialDatabaseAsync();

        var localPlacementPath = Path.Combine(_appPaths.BaseDirectory, "window-placement.json");
        Directory.CreateDirectory(_appPaths.BaseDirectory);
        await File.WriteAllTextAsync(localPlacementPath, "{\"LocalMachine\":true,\"Width\":1024}");

        var backupResult = await _backupService.CreateBackupAsync(
            Path.Combine(_testDir, "Backups"),
            null,
            default);
        Assert.True(backupResult.Success);

        // Artificially inject a window-placement.json into the backup zip
        var injectedBackupPath = Path.Combine(_testDir, "injected_placement.drxbackup");
        File.Copy(backupResult.BackupFilePath!, injectedBackupPath);

        using (var archive = ZipFile.Open(injectedBackupPath, ZipArchiveMode.Update))
        {
            var entry = archive.CreateEntry("settings/window-placement.json");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("{\"ForeignMachine\":true,\"Width\":1920}");
        }

        var restoreResult = await _restoreService.RestoreFromBackupAsync(injectedBackupPath);
        Assert.True(restoreResult.Success);

        // Local window placement file must NOT have been overwritten
        var livePlacement = await File.ReadAllTextAsync(localPlacementPath);
        Assert.Contains("LocalMachine", livePlacement);
        Assert.DoesNotContain("ForeignMachine", livePlacement);
    }
}
