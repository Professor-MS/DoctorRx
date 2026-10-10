using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Domain.ValueObjects;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using DoctorRx.Presentation;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using DoctorRx.Presentation.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class NavigationSmokeTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestDbContextFactory _factory;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;
    private int _seedPrescriptionId;

    public NavigationSmokeTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_SmokeNav_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        var dbPath = Path.Combine(_testDir, "smoke.db");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(builder.ConnectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _factory = new TestDbContextFactory(options);
        _uowFactory = new UnitOfWorkFactory(_factory);
        _clock = new SystemClock();

        using (var db = _factory.CreateDbContext())
        {
            db.Database.Migrate();

            var doctor = new Doctor
            {
                Name = "Dr. Asim Khan",
                Qualification = "MBBS, FCPS",
                ClinicName = "Al-Shifa Clinic",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.Doctors.Add(doctor);

            var patient = new Patient
            {
                Name = "Ahmad Raza",
                Age = 35,
                Gender = Gender.Male,
                Phone = "03001234567",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.Patients.Add(patient);
            db.SaveChanges();

            var prescription = Prescription.CreateFinalized(
                "RX-20261010-0001",
                patient.Id,
                doctor.Id,
                DateOnly.FromDateTime(DateTime.Today),
                doctor.ToSnapshot(),
                patient.ToSnapshot(DateOnly.FromDateTime(DateTime.Today)),
                DateTime.UtcNow,
                chiefComplaints: "Fever and cough");
            db.Prescriptions.Add(prescription);
            db.SaveChanges();
            _seedPrescriptionId = prescription.Id;
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void AllViewsAndWindows_InstantiateCleanlyOnStaThread()
    {
        StaTestRunner.Run(() =>
        {
            var dashboardView = new DashboardView();
            Assert.NotNull(dashboardView);

            var newPrescriptionView = new NewPrescriptionView();
            Assert.NotNull(newPrescriptionView);

            var patientsView = new PatientsView();
            Assert.NotNull(patientsView);

            var medicinesView = new MedicinesView();
            Assert.NotNull(medicinesView);

            var settingsView = new SettingsView();
            Assert.NotNull(settingsView);

            var prescriptionDetailView = new PrescriptionDetailView();
            Assert.NotNull(prescriptionDetailView);

            var scaffoldedFeatureView = new ScaffoldedFeatureView();
            Assert.NotNull(scaffoldedFeatureView);

            var prescriptionPaperView = new PrescriptionPaperView();
            Assert.NotNull(prescriptionPaperView);

            var dialogWindow = new CustomDialogWindow("Test Dialog", "Smoke test content", CustomDialogWindow.DialogType.Information);
            Assert.NotNull(dialogWindow);

            var printPreviewWindow = new PrintPreviewWindow(null);
            Assert.NotNull(printPreviewWindow);
        });
    }

    [Fact]
    public void MainWindow_NavigatesToEveryDestination_WithFullLayoutPass()
    {
        StaTestRunner.Run(() =>
        {
            var services = new ServiceCollection();
            BuildContainer(services);
            using var sp = services.BuildServiceProvider();

            var navService = sp.GetRequiredService<INavigationService>();
            var mainWindowVm = sp.GetRequiredService<MainWindowViewModel>();
            var placementService = sp.GetRequiredService<IWindowPlacementService>();
            var window = new MainWindow(mainWindowVm, placementService);

            window.Width = 1280;
            window.Height = 800;

            foreach (NavigationDestination destination in Enum.GetValues<NavigationDestination>())
            {
                if (destination == NavigationDestination.PrescriptionDetail)
                {
                    navService.NavigateTo(destination, _seedPrescriptionId);
                }
                else
                {
                    navService.NavigateTo(destination);
                }

                Assert.Equal(destination, navService.CurrentDestination);
                Assert.NotNull(mainWindowVm.CurrentView);

                // Force layout calculation (Measure and Arrange)
                window.Measure(new Size(1280, 800));
                window.Arrange(new Rect(0, 0, 1280, 800));
                window.UpdateLayout();
            }
        });
    }

    [Fact]
    public void AllDrawersAndModalOverlays_ToggleAndRenderWithoutException()
    {
        StaTestRunner.Run(() =>
        {
            var services = new ServiceCollection();
            BuildContainer(services);
            using var sp = services.BuildServiceProvider();

            var mainWindowVm = sp.GetRequiredService<MainWindowViewModel>();
            var placementService = sp.GetRequiredService<IWindowPlacementService>();
            var window = new MainWindow(mainWindowVm, placementService);

            window.Width = 1280;
            window.Height = 800;

            // 1. Doctor Setup Modal
            mainWindowVm.OpenDoctorSetupCommand.Execute(null);
            Assert.True(mainWindowVm.IsDoctorSetupOpen);
            window.Measure(new Size(1280, 800));
            window.Arrange(new Rect(0, 0, 1280, 800));
            window.UpdateLayout();

            mainWindowVm.CloseDoctorSetupCommand.Execute(null);
            Assert.False(mainWindowVm.IsDoctorSetupOpen);

            // 2. Help Shortcuts Overlay
            mainWindowVm.ToggleHelpOverlayCommand.Execute(null);
            Assert.True(mainWindowVm.IsHelpOverlayOpen);
            window.Measure(new Size(1280, 800));
            window.Arrange(new Rect(0, 0, 1280, 800));
            window.UpdateLayout();

            mainWindowVm.ToggleHelpOverlayCommand.Execute(null);
            Assert.False(mainWindowVm.IsHelpOverlayOpen);

            // 3. Patients View Drawer
            var patientsVm = sp.GetRequiredService<PatientsViewModel>();
            var patientsView = new PatientsView { DataContext = patientsVm };
            patientsVm.OpenNewPatientDrawerCommand.Execute(null);
            Assert.True(patientsVm.IsDrawerOpen);
            patientsView.Measure(new Size(1280, 800));
            patientsView.Arrange(new Rect(0, 0, 1280, 800));
            patientsView.UpdateLayout();

            patientsVm.CloseDrawerCommand.Execute(null);
            Assert.False(patientsVm.IsDrawerOpen);

            // 4. Medicines View Drawer
            var medicinesVm = sp.GetRequiredService<MedicinesViewModel>();
            var medicinesView = new MedicinesView { DataContext = medicinesVm };
            medicinesVm.OpenAddDrawerCommand.Execute(null);
            Assert.True(medicinesVm.IsDrawerOpen);
            medicinesView.Measure(new Size(1280, 800));
            medicinesView.Arrange(new Rect(0, 0, 1280, 800));
            medicinesView.UpdateLayout();

            medicinesVm.CloseDrawerCommand.Execute(null);
            Assert.False(medicinesVm.IsDrawerOpen);
        });
    }

    private void BuildContainer(IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, StubDialogService>();
        services.AddSingleton<IWindowPlacementService, StubWindowPlacementService>();
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton<IPatientsFilterSessionService, PatientsFilterSessionService>();
        services.AddSingleton<IQuickPhrasesService, StubQuickPhrasesService>();
        services.AddSingleton<IPrescriptionComposerValidator>(_ => new PrescriptionComposerValidator(_clock));

        // Real application domain & infrastructure services
        var draftService = new DraftService(_uowFactory, _clock, NullLogger<DraftService>.Instance);
        var prescriptionService = new PrescriptionService(_uowFactory, _clock, NullLogger<PrescriptionService>.Instance, draftService);
        var patientService = new PatientService(_uowFactory, _clock, NullLogger<PatientService>.Instance);
        var medicineService = new MedicineService(_uowFactory, _clock, NullLogger<MedicineService>.Instance);
        var doctorService = new DoctorService(_uowFactory, _clock, NullLogger<DoctorService>.Instance);
        var dashboardService = new DashboardService(_uowFactory, _clock);

        services.AddSingleton<IDraftService>(draftService);
        services.AddSingleton<IPrescriptionService>(prescriptionService);
        services.AddSingleton<IPatientService>(patientService);
        services.AddSingleton<IMedicineService>(medicineService);
        services.AddSingleton<IDoctorService>(doctorService);
        services.AddSingleton<IDashboardService>(dashboardService);

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PatientsViewModel>(sp => new PatientsViewModel(
            sp.GetRequiredService<IPatientService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<IPatientsFilterSessionService>(),
            NullLogger<PatientsViewModel>.Instance));
        services.AddTransient<NewPrescriptionViewModel>(sp => new NewPrescriptionViewModel(
            sp.GetRequiredService<IPatientService>(),
            sp.GetRequiredService<IMedicineService>(),
            sp.GetRequiredService<IPrescriptionService>(),
            sp.GetRequiredService<IDraftService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<IPrescriptionComposerValidator>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IQuickPhrasesService>()));
        services.AddTransient<PrescriptionHistoryViewModel>();
        services.AddTransient<MedicinesViewModel>(sp => new MedicinesViewModel(
            sp.GetRequiredService<IMedicineService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<INavigationService>(),
            NullLogger<MedicinesViewModel>.Instance));
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PrescriptionDetailViewModel>(sp => new PrescriptionDetailViewModel(
            sp.GetRequiredService<IPrescriptionService>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<IDialogService>()));
    }

    private class StubDialogService : IDialogService
    {
        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
        public bool ShowConfirmation(string title, string message) => true;
        public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel") => true;
    }

    private class StubWindowPlacementService : IWindowPlacementService
    {
        public WindowPlacementSettings? LoadPlacement() => null;
        public void SavePlacement(WindowPlacementSettings settings) { }
        public void ApplyPlacement(Window window) { }
        public void PersistPlacement(Window window, bool? isSidebarCollapsedOverride = null) { }
        public bool IsOnScreen(double left, double top, double width, double height) => true;
    }

    private class StubQuickPhrasesService : IQuickPhrasesService
    {
        public Task<IReadOnlyList<string>> GetQuickPhrasesAsync() =>
            Task.FromResult<IReadOnlyList<string>>(new List<string> { "Take after meals" });
        public Task SaveQuickPhrasesAsync(IEnumerable<string> phrases) => Task.CompletedTask;
    }
}
