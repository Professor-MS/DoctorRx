using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class ComposerSearchIntegrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly IDbContextFactory<DoctorRxDbContext> _factory;

    public ComposerSearchIntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_ComposerSearch_" + Guid.NewGuid().ToString("N"));
        _appPaths = new TestAppPaths(_testDir);

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

        var migrator = new DatabaseMigrator(_factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        migrator.MigrateDatabaseAsync().GetAwaiter().GetResult();
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
        }
    }

    [Fact]
    public async Task ComposerSearch_EmptyQuery_LoadsRecentPatientsInitially()
    {
        await using (var context = await _factory.CreateDbContextAsync())
        {
            for (int i = 1; i <= 15; i++)
            {
                var p = new Patient
                {
                    Name = $"Recent Patient {i}",
                    RecordNumber = $"P-{i:D6}",
                    DateOfBirth = new DateOnly(1990, 1, 1),
                    Gender = Gender.Male,
                    Phone = $"0300{i:D7}",
                    LastVisitDate = new DateOnly(2026, 1, 1).AddDays(i),
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-i)
                };
                p.RefreshSearchFields();
                context.Patients.Add(p);
            }
            await context.SaveChangesAsync();
        }

        var uowFactory = new UnitOfWorkFactory(_factory);
        var clock = new SystemClock();
        var draftService = new DraftService(uowFactory, clock, NullLogger<DraftService>.Instance);
        var patientService = new PatientService(uowFactory, clock, NullLogger<PatientService>.Instance);
        var medicineService = new MedicineService(uowFactory, clock, NullLogger<MedicineService>.Instance);
        var prescriptionService = new PrescriptionService(uowFactory, clock, NullLogger<PrescriptionService>.Instance, draftService);
        var dialogService = new TestDialogService();
        var navService = new TestNavigationService();
        var validator = new PrescriptionComposerValidator(clock);

        var vm = new NewPrescriptionViewModel(
            patientService,
            medicineService,
            prescriptionService,
            draftService,
            dialogService,
            navService,
            validator,
            clock);

        // Allow async initial load in constructor to complete
        for (int i = 0; i < 40 && vm.PatientSearchResults.Count == 0; i++)
        {
            await Task.Delay(50);
        }

        Assert.Equal(10, vm.PatientSearchResults.Count);
        Assert.False(vm.NoPatientFound);
        Assert.Equal("Recent patients", vm.SearchResultCountText);
    }

    [Fact]
    public async Task ComposerSearch_TypingUnknownPatient_ShowsEmptyStateAndPreFillsRegistration()
    {
        var uowFactory = new UnitOfWorkFactory(_factory);
        var clock = new SystemClock();
        var draftService = new DraftService(uowFactory, clock, NullLogger<DraftService>.Instance);
        var patientService = new PatientService(uowFactory, clock, NullLogger<PatientService>.Instance);
        var medicineService = new MedicineService(uowFactory, clock, NullLogger<MedicineService>.Instance);
        var prescriptionService = new PrescriptionService(uowFactory, clock, NullLogger<PrescriptionService>.Instance, draftService);
        var dialogService = new TestDialogService();
        var navService = new TestNavigationService();
        var validator = new PrescriptionComposerValidator(clock);

        var vm = new NewPrescriptionViewModel(
            patientService,
            medicineService,
            prescriptionService,
            draftService,
            dialogService,
            navService,
            validator,
            clock);

        // Type query that does not exist
        vm.PatientSearchQuery = "Unknown Nonexistent Person";

        // Wait for 250ms debounce and search to complete
        for (int i = 0; i < 40 && (!vm.NoPatientFound || vm.IsPatientSearching); i++)
        {
            await Task.Delay(50);
        }

        Assert.Empty(vm.PatientSearchResults);
        Assert.True(vm.NoPatientFound);
        Assert.Equal("No patient found for 'Unknown Nonexistent Person'", vm.NoPatientFoundText);

        // Trigger RegisterNewPatientFromSearchCommand
        vm.RegisterNewPatientFromSearchCommand.Execute(null);

        Assert.True(vm.IsQuickRegisterDrawerOpen);
        Assert.Equal("Unknown Nonexistent Person", vm.NewPatientName);
    }

    [Fact]
    public async Task ComposerSearch_RapidKeystrokes_CancelsPreviousQueriesSafely()
    {
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var p1 = new Patient { Name = "Ali Raza", RecordNumber = "P-000001", DateOfBirth = new DateOnly(1990, 1, 1), Gender = Gender.Male };
            var p2 = new Patient { Name = "Alia Bhatt", RecordNumber = "P-000002", DateOfBirth = new DateOnly(1992, 1, 1), Gender = Gender.Female };
            p1.RefreshSearchFields();
            p2.RefreshSearchFields();
            context.Patients.AddRange(p1, p2);
            await context.SaveChangesAsync();
        }

        var uowFactory = new UnitOfWorkFactory(_factory);
        var clock = new SystemClock();
        var draftService = new DraftService(uowFactory, clock, NullLogger<DraftService>.Instance);
        var patientService = new PatientService(uowFactory, clock, NullLogger<PatientService>.Instance);
        var medicineService = new MedicineService(uowFactory, clock, NullLogger<MedicineService>.Instance);
        var prescriptionService = new PrescriptionService(uowFactory, clock, NullLogger<PrescriptionService>.Instance, draftService);
        var dialogService = new TestDialogService();
        var navService = new TestNavigationService();
        var validator = new PrescriptionComposerValidator(clock);

        var vm = new NewPrescriptionViewModel(
            patientService,
            medicineService,
            prescriptionService,
            draftService,
            dialogService,
            navService,
            validator,
            clock);

        // Rapid keystrokes within debounce interval
        vm.PatientSearchQuery = "A";
        await Task.Delay(50);
        vm.PatientSearchQuery = "Al";
        await Task.Delay(50);
        vm.PatientSearchQuery = "Ali";
        await Task.Delay(50);
        vm.PatientSearchQuery = "Ali Raza";

        // Wait for final debounce and search to complete
        for (int i = 0; i < 40 && (vm.PatientSearchResults.Count != 1 || vm.IsPatientSearching); i++)
        {
            await Task.Delay(50);
        }

        Assert.Single(vm.PatientSearchResults);
        Assert.Equal("Ali Raza", vm.PatientSearchResults[0].Name);
        Assert.False(vm.NoPatientFound);
        Assert.Equal("1 result found", vm.SearchResultCountText);
    }

    private class TestDialogService : IDialogService
    {
        public bool ShowConfirmation(string title, string message) => true;
        public bool? ShowConfirmationWithCancel(string title, string message) => true;
        public void ShowError(string title, string message) { }
        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
    }

    private class TestNavigationService : INavigationService
    {
        public ViewModelBase? CurrentViewModel { get; set; }
        public NavigationDestination CurrentDestination { get; set; }
#pragma warning disable CS0067
        public event Action<ViewModelBase>? CurrentViewModelChanged;
#pragma warning restore CS0067
        public void NavigateTo(NavigationDestination destination, object? parameter = null)
        {
            CurrentDestination = destination;
        }
    }
}
