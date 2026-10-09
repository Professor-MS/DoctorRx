using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using DoctorRx.Presentation.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class SettingsAndDashboardTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly PhysicalFileSystem _fileSystem;
    private readonly TestDbContextFactory _factory;
    private readonly IClock _clock;
    private readonly BackupService _backupService;
    private readonly DatabaseMigrator _migrator;
    private readonly RestoreService _restoreService;
    private readonly AutoBackupService _autoBackupService;
    private readonly DatabaseHealthService _healthService;
    private readonly TestDialogService _dialogService;
    private readonly TestNavigationService _navigationService;
    private readonly TestDashboardService _dashboardService;
    private readonly TestDraftService _draftService;

    public SettingsAndDashboardTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_SettingsTests_" + Guid.NewGuid().ToString("N"));
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
        _restoreService = new RestoreService(_factory, _appPaths, _fileSystem, _backupService, _migrator, _clock, NullLogger<RestoreService>.Instance);
        _autoBackupService = new AutoBackupService(_backupService, _appPaths, _fileSystem, _clock, NullLogger<AutoBackupService>.Instance);
        var searchRepair = new SearchIndexRepairService(_factory, NullLogger<SearchIndexRepairService>.Instance);
        _healthService = new DatabaseHealthService(_factory, _appPaths, _fileSystem, _clock, searchRepair, NullLogger<DatabaseHealthService>.Instance);

        _dialogService = new TestDialogService();
        _navigationService = new TestNavigationService();
        _dashboardService = new TestDashboardService();
        _draftService = new TestDraftService();
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
            // Best effort
        }
    }

    private async Task SeedDatabaseAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();

        var doc = new Doctor
        {
            Name = "Dr. Test Doctor",
            Qualification = "MBBS",
            RegistrationNumber = "PMC-1111",
            Specialization = "Physician",
            ClinicName = "Test Clinic",
            IsActive = true
        };
        context.Doctors.Add(doc);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task SettingsViewModel_InitializesMetricsAndBackups()
    {
        await SeedDatabaseAsync();

        var vm = new SettingsViewModel(
            _backupService,
            _restoreService,
            _autoBackupService,
            _healthService,
            _appPaths,
            _dialogService,
            _clock);

        await vm.InitializeAsync();

        Assert.Equal(_appPaths.DatabasePath, vm.DatabasePath);
        Assert.True(vm.IsHealthy);
        Assert.Contains("Healthy", vm.HealthSummary);
        Assert.NotEmpty(vm.DatabaseSizeText);
        Assert.NotEmpty(vm.FreeSpaceText);
    }

    [Fact]
    public async Task SettingsViewModel_BackupNow_CreatesBackupAndRefreshes()
    {
        await SeedDatabaseAsync();

        var vm = new SettingsViewModel(
            _backupService,
            _restoreService,
            _autoBackupService,
            _healthService,
            _appPaths,
            _dialogService,
            _clock);

        await vm.InitializeAsync();
        Assert.Empty(vm.Backups);

        await ((IAsyncRelayCommand)vm.BackupNowCommand).ExecuteAsync(null);

        Assert.Single(vm.Backups);
        Assert.True(File.Exists(vm.Backups[0].FilePath));
        Assert.Contains(_dialogService.InformationMessages, m => m.Title.Contains("Backup Complete"));
    }

    [Fact]
    public async Task SettingsViewModel_SavesBackupSettings()
    {
        var vm = new SettingsViewModel(
            _backupService,
            _restoreService,
            _autoBackupService,
            _healthService,
            _appPaths,
            _dialogService,
            _clock);

        vm.KeepDailyCount = 14;
        vm.KeepWeeklyCount = 8;
        vm.KeepMonthlyCount = 6;
        vm.AutoBackupDaily = true;

        await ((IAsyncRelayCommand)vm.SaveBackupSettingsCommand).ExecuteAsync(null);

        var saved = _autoBackupService.GetSettings();
        Assert.Equal(14, saved.DailyRetention);
        Assert.Equal(8, saved.WeeklyRetention);
        Assert.Equal(6, saved.MonthlyRetention);
        Assert.True(saved.AutoBackupEnabled);
    }

    [Fact]
    public async Task DashboardViewModel_LoadsBackupStatusAndTriggersBackup()
    {
        await SeedDatabaseAsync();

        var vm = new DashboardViewModel(
            _dashboardService,
            _navigationService,
            _draftService,
            _autoBackupService,
            _backupService,
            _appPaths);

        await vm.InitializeAsync();

        // Initially no backups exist
        Assert.False(string.IsNullOrWhiteSpace(vm.LastBackupText));
        Assert.False(string.IsNullOrWhiteSpace(vm.BackupBadgeText));

        // Trigger backup from Dashboard
        await ((IAsyncRelayCommand)vm.BackupNowDashboardCommand).ExecuteAsync(null);

        // Now backup exists
        Assert.True(vm.IsBackupHealthy);
        Assert.Equal("Protected", vm.BackupBadgeText);
    }

    [Fact]
    public void SettingsView_CanBeInstantiated_OnStaThread()
    {
        StaTestRunner.Run(() =>
        {
            var vm = new SettingsViewModel(
                _backupService,
                _restoreService,
                _autoBackupService,
                _healthService,
                _appPaths,
                _dialogService,
                _clock);

            var view = new SettingsView { DataContext = vm };
            Assert.NotNull(view);
        });
    }
}

public class TestDialogService : IDialogService
{
    public System.Collections.Generic.List<(string Title, string Message)> InformationMessages { get; } = new();
    public System.Collections.Generic.List<(string Title, string Message)> WarningMessages { get; } = new();
    public System.Collections.Generic.List<(string Title, string Message)> ErrorMessages { get; } = new();
    public bool ConfirmationResult { get; set; } = true;

    public void ShowInformation(string title, string message) => InformationMessages.Add((title, message));
    public void ShowWarning(string title, string message) => WarningMessages.Add((title, message));
    public void ShowError(string title, string message) => ErrorMessages.Add((title, message));
    public bool ShowConfirmation(string title, string message) => ConfirmationResult;
    public bool ShowConfirmation(string title, string message, string confirmText, string cancelText) => ConfirmationResult;
    public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel") => ConfirmationResult;
}

public class TestNavigationService : INavigationService
{
    public ViewModelBase? CurrentViewModel { get; set; }
    public NavigationDestination CurrentDestination { get; set; } = NavigationDestination.Dashboard;
    public event Action<ViewModelBase>? CurrentViewModelChanged { add { } remove { } }

    public void NavigateTo(NavigationDestination destination, object? parameter = null)
    {
        CurrentDestination = destination;
    }
}

public class TestDashboardService : IDashboardService
{
    public Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DashboardStatsDto(
            TotalPatients: 15,
            PrescriptionsToday: 3,
            TotalPrescriptions: 42,
            TotalMedicinesInCatalog: 120,
            RecentPrescriptions: new System.Collections.Generic.List<PrescriptionSummaryDto>(),
            RecentPatients: new System.Collections.Generic.List<PatientDto>()
        ));
    }
}

public class TestDraftService : IDraftService
{
    public Task<int> GetCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<System.Collections.Generic.IReadOnlyList<DraftSummaryDto>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<System.Collections.Generic.IReadOnlyList<DraftSummaryDto>>(new System.Collections.Generic.List<DraftSummaryDto>());
    public Task<DoctorRx.Application.Common.Result<PrescriptionComposerState>> GetAsync(Guid draftKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(DoctorRx.Application.Common.Result<PrescriptionComposerState>.Failure("Not found"));
    public Task<DoctorRx.Application.Common.Result<Guid>> SaveAsync(PrescriptionComposerState state, CancellationToken cancellationToken = default) =>
        Task.FromResult(DoctorRx.Application.Common.Result<Guid>.Success(state.DraftKey));
    public Task<DoctorRx.Application.Common.Result> DiscardAsync(Guid draftKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(DoctorRx.Application.Common.Result.Success());
    public void MarkFinalized(Guid draftKey) { }
    public bool IsFinalized(Guid draftKey) => false;
}
