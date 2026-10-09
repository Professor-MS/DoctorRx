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
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class BackupEngineTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly PhysicalFileSystem _fileSystem;
    private readonly TestDbContextFactory _factory;
    private readonly IClock _clock;

    public BackupEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_BackupTests_" + Guid.NewGuid().ToString("N"));
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
            // Best effort temp cleanup
        }
    }

    private async Task SeedDatabaseAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();

        var doctor = new Doctor
        {
            Name = "Dr. Farooq Khan",
            Qualification = "MBBS, FCPS",
            RegistrationNumber = "PMC-12345",
            Specialization = "Cardiologist",
            ClinicName = "Heart Care Clinic",
            IsActive = true
        };
        context.Doctors.Add(doctor);

        var patient = new Patient
        {
            Name = "Ahmed Ali",
            NormalizedName = "ahmed ali",
            RecordNumber = "MR-00100",
            Gender = Gender.Male,
            Phone = "03009876543",
            PhoneDigits = "03009876543"
        };
        context.Patients.Add(patient);

        var medicine = new Medicine
        {
            Name = "Atorvastatin",
            NormalizedName = "atorvastatin",
            Form = "Tab",
            Strength = "20mg",
            GenericName = "Atorvastatin"
        };
        context.Medicines.Add(medicine);

        await context.SaveChangesAsync();

        var date = DateOnly.FromDateTime(DateTime.Today);
        var rx = Prescription.CreateFinalized(
            prescriptionNumber: "RX-20261009-0001",
            patientId: patient.Id,
            doctorId: doctor.Id,
            prescriptionDate: date,
            doctorSnapshot: doctor.ToSnapshot(),
            patientSnapshot: patient.ToSnapshot(date),
            finalizedAtUtc: DateTime.UtcNow,
            chiefComplaints: "Chest tightness"
        );
        rx.AddMedicine(new PrescriptionMedicine
        {
            MedicineName = medicine.Name,
            GenericName = medicine.GenericName,
            Form = medicine.Form,
            Strength = medicine.Strength,
            Dose = "1 tab",
            Frequency = "At bedtime",
            Route = "Oral",
            Duration = "30 days"
        });

        context.Prescriptions.Add(rx);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBackupAsync_RoundTrip_SucceedsAndPassesImmediateVerification()
    {
        // Arrange
        await SeedDatabaseAsync();

        // Create a dummy custom settings file and a window placement file
        var customSettingsPath = Path.Combine(_appPaths.BaseDirectory, "print-settings.json");
        var windowPlacementPath = Path.Combine(_appPaths.BaseDirectory, "window-placement.json");
        await File.WriteAllTextAsync(customSettingsPath, "{\"paperSize\": \"A4\"}");
        await File.WriteAllTextAsync(windowPlacementPath, "{\"x\": 100, \"y\": 100}");

        var backupDir = Path.Combine(_testDir, "TargetBackups");
        var service = new BackupService(_factory, _appPaths, _fileSystem, _clock, NullLogger<BackupService>.Instance);

        // Act
        var result = await service.CreateBackupAsync(backupDir);

        // Assert
        Assert.True(result.Success, $"Backup failed: {result.ErrorMessage}");
        Assert.NotNull(result.BackupFilePath);
        Assert.True(File.Exists(result.BackupFilePath));
        Assert.True(result.IsVerified);
        Assert.NotNull(result.Manifest);

        // Verify Manifest details (counts only, no PII)
        var manifest = result.Manifest;
        Assert.Equal(1, manifest.FormatVersion);
        Assert.Equal("1.0.0", manifest.AppVersion);
        Assert.Equal(1, manifest.TableRowCounts["Patients"]);
        Assert.Equal(1, manifest.TableRowCounts["Doctors"]);
        Assert.Equal(1, manifest.TableRowCounts["Medicines"]);
        Assert.Equal(1, manifest.TableRowCounts["Prescriptions"]);
        Assert.Equal(1, manifest.TableRowCounts["PrescriptionMedicines"]);
        Assert.False(string.IsNullOrWhiteSpace(manifest.DatabaseSha256));

        // Inspect ZIP contents
        using var zip = ZipFile.OpenRead(result.BackupFilePath);
        Assert.NotNull(zip.GetEntry("snapshot.db"));
        Assert.NotNull(zip.GetEntry("manifest.json"));
        Assert.NotNull(zip.GetEntry("settings/print-settings.json"));

        // Crucial test rule: window-placement.json must NEVER be bundled
        Assert.Null(zip.GetEntry("settings/window-placement.json"));

        // Verify independent verification call
        var verification = await service.VerifyBackupAsync(result.BackupFilePath);
        Assert.True(verification.IsValid);
        Assert.Equal("Verified", verification.StatusSummary);
        Assert.True(verification.HashMatched);
        Assert.True(verification.RowCountsMatched);
    }

    [Fact]
    public async Task CreateBackupAsync_InsufficientDiskSpace_FailsWithFriendlyMessage()
    {
        // Arrange
        await SeedDatabaseAsync();
        var fakeFs = new LowSpaceFakeFileSystem(_fileSystem, simulatedFreeBytes: 100); // Only 100 bytes free
        var service = new BackupService(_factory, _appPaths, fakeFs, _clock, NullLogger<BackupService>.Instance);

        // Act
        var result = await service.CreateBackupAsync(Path.Combine(_testDir, "Backups"));

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Not enough free disk space", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateBackupAsync_WhileWritesOccur_ProducesConsistentBackup()
    {
        // Arrange
        await SeedDatabaseAsync();
        var backupDir = Path.Combine(_testDir, "ConcurrentBackups");
        var service = new BackupService(_factory, _appPaths, _fileSystem, _clock, NullLogger<BackupService>.Instance);

        using var cts = new CancellationTokenSource();

        // Background write task simulating concurrent physician edits
        var writeTask = Task.Run(async () =>
        {
            int counter = 0;
            while (!cts.Token.IsCancellationRequested && counter < 50)
            {
                try
                {
                    await using var ctx = await _factory.CreateDbContextAsync(cts.Token);
                    ctx.Patients.Add(new Patient
                    {
                        Name = $"Concurrent Patient {counter}",
                        NormalizedName = $"concurrent patient {counter}",
                        RecordNumber = $"MR-C-{counter}",
                        Gender = Gender.Other
                    });
                    await ctx.SaveChangesAsync(cts.Token);
                    counter++;
                    await Task.Delay(5, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        // Act: Run backup while writes are ongoing
        var result = await service.CreateBackupAsync(backupDir);

        cts.Cancel();
        await writeTask;

        // Assert
        Assert.True(result.Success, $"Backup failed during concurrent writes: {result.ErrorMessage}");
        Assert.True(result.IsVerified);

        var verify = await service.VerifyBackupAsync(result.BackupFilePath!);
        Assert.True(verify.IsValid);
    }

    [Fact]
    public async Task Backup_FileNameAndManifest_ContainZeroPII()
    {
        // Arrange
        await SeedDatabaseAsync();
        var backupDir = Path.Combine(_testDir, "PiiBackups");
        var service = new BackupService(_factory, _appPaths, _fileSystem, _clock, NullLogger<BackupService>.Instance);

        // Act
        var result = await service.CreateBackupAsync(backupDir);
        Assert.True(result.Success);

        // Assert 1: File name contains no patient info
        var fileName = Path.GetFileName(result.BackupFilePath!);
        Assert.StartsWith("DoctorRx-Backup-", fileName);
        Assert.EndsWith(".drxbackup", fileName);
        Assert.DoesNotContain("Ahmed", fileName);
        Assert.DoesNotContain("03009876543", fileName);
        Assert.DoesNotContain("Atorvastatin", fileName);

        // Assert 2: Manifest contains no patient info
        var manifestJson = JsonSerializer.Serialize(result.Manifest);
        Assert.DoesNotContain("Ahmed", manifestJson);
        Assert.DoesNotContain("03009876543", manifestJson);
        Assert.DoesNotContain("Atorvastatin", manifestJson);
        Assert.DoesNotContain("Chest tightness", manifestJson);
    }

    private class LowSpaceFakeFileSystem : IFileSystem
    {
        private readonly IFileSystem _inner;
        private readonly long _simulatedFreeBytes;

        public LowSpaceFakeFileSystem(IFileSystem inner, long simulatedFreeBytes)
        {
            _inner = inner;
            _simulatedFreeBytes = simulatedFreeBytes;
        }

        public bool FileExists(string path) => _inner.FileExists(path);
        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public void CreateDirectory(string path) => _inner.CreateDirectory(path);
        public void DeleteFile(string path) => _inner.DeleteFile(path);
        public void CopyFile(string source, string destination, bool overwrite) => _inner.CopyFile(source, destination, overwrite);
        public void MoveFile(string source, string destination, bool overwrite) => _inner.MoveFile(source, destination, overwrite);
        public long GetFileSize(string path) => _inner.GetFileSize(path);
        public long GetAvailableFreeSpace(string path) => _simulatedFreeBytes;
        public Stream OpenRead(string path) => _inner.OpenRead(path);
        public Stream OpenWrite(string path) => _inner.OpenWrite(path);
        public Stream CreateFile(string path) => _inner.CreateFile(path);
        public string ReadAllText(string path) => _inner.ReadAllText(path);
        public void WriteAllText(string path, string content) => _inner.WriteAllText(path, content);
        public IEnumerable<string> GetFiles(string path, string searchPattern) => _inner.GetFiles(path, searchPattern);
        public DateTime GetCreationTimeUtc(string path) => _inner.GetCreationTimeUtc(path);
    }
}
