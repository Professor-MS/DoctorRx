using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class BackupRetentionTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly PhysicalFileSystem _fileSystem;

    public BackupRetentionTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_RetentionTests_" + Guid.NewGuid().ToString("N"));
        _appPaths = new TestAppPaths(_testDir);
        _fileSystem = new PhysicalFileSystem();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best effort temp cleanup
        }
    }

    [Fact]
    public void EvaluateBackupsToPrune_GFS_RetainsExpectedCounts()
    {
        // Arrange: Generate 45 backups across 45 consecutive days
        var now = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
        var backups = new List<BackupMetadataDto>();

        for (int i = 0; i < 45; i++)
        {
            var date = now.AddDays(-i);
            backups.Add(new BackupMetadataDto(
                FilePath: $"C:/fake/DoctorRx-Backup-{date:yyyy-MM-dd-HHmm}.drxbackup",
                FileName: $"DoctorRx-Backup-{date:yyyy-MM-dd-HHmm}.drxbackup",
                FileSizeBytes: 1024 * 1024,
                CreatedAtUtc: date,
                IsVerified: true,
                Manifest: null
            ));
        }

        // Act: GFS with 7 daily, 4 weekly, 3 monthly
        var plan = BackupRetentionPolicy.EvaluateBackupsToPrune(backups, dailyCount: 7, weeklyCount: 4, monthlyCount: 3);

        // Assert
        Assert.NotEmpty(plan.KeptBackups);
        Assert.NotEmpty(plan.BackupsToDelete);

        // Daily rule: must keep at least the last 7 distinct days
        var recent7Days = backups.Take(7).Select(b => b.FilePath).ToHashSet();
        foreach (var path in recent7Days)
        {
            Assert.Contains(plan.KeptBackups, b => b.FilePath == path);
        }

        // Kept count should be <= 7 daily + 4 weekly + 3 monthly (some overlap occurs)
        Assert.True(plan.KeptBackups.Count <= 14);
        Assert.True(plan.KeptBackups.Count >= 7);
        Assert.Equal(45, plan.KeptBackups.Count + plan.BackupsToDelete.Count);
    }

    [Fact]
    public void EvaluateBackupsToPrune_NeverDeletesTheOnlyVerifiedBackup()
    {
        // Arrange: 10 recent unverified backups, and 1 old (50-day-old) verified backup
        var now = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
        var backups = new List<BackupMetadataDto>();

        // 10 unverified recent backups (days 0 to 9)
        for (int i = 0; i < 10; i++)
        {
            backups.Add(new BackupMetadataDto(
                FilePath: $"C:/fake/recent_{i}.drxbackup",
                FileName: $"recent_{i}.drxbackup",
                FileSizeBytes: 1024,
                CreatedAtUtc: now.AddDays(-i),
                IsVerified: false,
                Manifest: null
            ));
        }

        // 1 ancient verified backup (day 50)
        var oldVerified = new BackupMetadataDto(
            FilePath: "C:/fake/old_verified.drxbackup",
            FileName: "old_verified.drxbackup",
            FileSizeBytes: 1024,
            CreatedAtUtc: now.AddDays(-50),
            IsVerified: true,
            Manifest: null
        );
        backups.Add(oldVerified);

        // Act: Evaluate with small daily/weekly/monthly limits where day 50 would normally be pruned
        var plan = BackupRetentionPolicy.EvaluateBackupsToPrune(backups, dailyCount: 3, weeklyCount: 1, monthlyCount: 1);

        // Assert: The 50-day-old verified backup must NOT be deleted because it is the ONLY verified backup
        Assert.Contains(plan.KeptBackups, b => b.FilePath == oldVerified.FilePath);
        Assert.DoesNotContain(plan.BackupsToDelete, b => b.FilePath == oldVerified.FilePath);
    }

    [Fact]
    public void EvaluateSafetyBackupsToPrune_KeepsLast5Separately()
    {
        // Arrange: 8 safety backups
        var now = DateTime.UtcNow;
        var safetyBackups = new List<BackupMetadataDto>();
        for (int i = 0; i < 8; i++)
        {
            safetyBackups.Add(new BackupMetadataDto(
                FilePath: $"C:/fake/safety_{i}.drxbackup",
                FileName: $"safety_{i}.drxbackup",
                FileSizeBytes: 2048,
                CreatedAtUtc: now.AddHours(-i),
                IsVerified: true,
                Manifest: null
            ));
        }

        // Act
        var plan = BackupRetentionPolicy.EvaluateSafetyBackupsToPrune(safetyBackups, maxSafetyBackups: 5);

        // Assert: Exactly 5 kept, 3 deleted
        Assert.Equal(5, plan.KeptBackups.Count);
        Assert.Equal(3, plan.BackupsToDelete.Count);

        // Newest 5 are kept
        for (int i = 0; i < 5; i++)
        {
            Assert.Contains(plan.KeptBackups, b => b.FilePath == safetyBackups[i].FilePath);
        }
        // Oldest 3 are pruned
        for (int i = 5; i < 8; i++)
        {
            Assert.Contains(plan.BackupsToDelete, b => b.FilePath == safetyBackups[i].FilePath);
        }
    }

    [Fact]
    public async Task AutoBackupService_ShouldRunShutdownBackup_RespectsIntervalAndSettings()
    {
        // Arrange
        var fakeClock = new TestClock(DateTime.UtcNow);
        var stubBackupService = new StubBackupService();
        var autoService = new AutoBackupService(stubBackupService, _appPaths, _fileSystem, fakeClock, NullLogger<AutoBackupService>.Instance);

        // 1. Initial state (no backup ever made): Should run
        Assert.True(await autoService.ShouldRunShutdownBackupAsync());

        // 2. Perform backup
        var runResult = await autoService.RunAutoBackupAsync();
        Assert.True(runResult.Success);

        // 3. Right after successful backup: Should NOT run (interval is 4 hours)
        Assert.False(await autoService.ShouldRunShutdownBackupAsync());

        // 4. Advance clock by 2 hours: Still should NOT run
        fakeClock.Advance(TimeSpan.FromHours(2));
        Assert.False(await autoService.ShouldRunShutdownBackupAsync());

        // 5. Advance clock by 3 more hours (total 5 hours >= 4 hours): SHOULD run
        fakeClock.Advance(TimeSpan.FromHours(3));
        Assert.True(await autoService.ShouldRunShutdownBackupAsync());

        // 6. If disabled in settings: Should NOT run
        var settings = autoService.GetSettings();
        settings.AutoBackupEnabled = false;
        await autoService.SaveSettingsAsync(settings);
        Assert.False(await autoService.ShouldRunShutdownBackupAsync());
    }

    [Fact]
    public void IsDestinationOnSameDriveAsDatabase_DetectsSameDriveCorrectly()
    {
        var stubBackup = new StubBackupService();
        var autoService = new AutoBackupService(stubBackup, _appPaths, _fileSystem, new TestClock(DateTime.UtcNow), NullLogger<AutoBackupService>.Instance);

        // Database is in _appPaths.DatabasePath
        var sameDir = Path.Combine(_appPaths.BaseDirectory, "AnotherFolder");
        Assert.True(autoService.IsDestinationOnSameDriveAsDatabase(sameDir));

        // Different drive (e.g. Z:\ or another root if different)
        var dbRoot = Path.GetPathRoot(Path.GetFullPath(_appPaths.DatabasePath));
        var differentDrive = dbRoot?.StartsWith("C", StringComparison.OrdinalIgnoreCase) == true ? "D:\\Backups" : "C:\\Backups";
        Assert.False(autoService.IsDestinationOnSameDriveAsDatabase(differentDrive));
    }

    private class TestClock : IClock
    {
        private DateTime _now;
        public TestClock(DateTime initial) => _now = initial;
        public DateTime UtcNow => _now;
        public DateTime Now => _now.ToLocalTime();
        public DateOnly Today => DateOnly.FromDateTime(_now);
        public void Advance(TimeSpan span) => _now = _now.Add(span);
    }

    private class StubBackupService : IBackupService
    {
        public Task<BackupResult> CreateBackupAsync(string targetDirectory, IProgress<double>? progress = null, System.Threading.CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(targetDirectory);
            var fakePath = Path.Combine(targetDirectory, $"DoctorRx-Backup-{DateTime.UtcNow:yyyy-MM-dd-HHmm}.drxbackup");
            File.WriteAllText(fakePath, "stub backup content");
            var manifest = new BackupManifest(1, "1.0.0", "Migration1", DateTime.UtcNow, new Dictionary<string, int>(), "hash", 100);
            return Task.FromResult(new BackupResult(true, fakePath, null, manifest, true));
        }

        public Task<BackupVerificationResult> VerifyBackupAsync(string backupFilePath, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BackupVerificationResult(true, "Verified", null, "ok", "ok", true, true));
        }

        public Task<IReadOnlyList<BackupMetadataDto>> GetAvailableBackupsAsync(string directory, System.Threading.CancellationToken cancellationToken = default)
        {
            var list = new List<BackupMetadataDto>();
            if (Directory.Exists(directory))
            {
                foreach (var f in Directory.GetFiles(directory, "*.drxbackup"))
                {
                    list.Add(new BackupMetadataDto(f, Path.GetFileName(f), 100, File.GetCreationTimeUtc(f), true, null));
                }
            }
            return Task.FromResult<IReadOnlyList<BackupMetadataDto>>(list);
        }
    }
}
