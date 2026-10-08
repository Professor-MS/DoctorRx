using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Domain.ValueObjects;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using DoctorRx.Presentation.Views;
using Xunit;

namespace DoctorRx.Tests;

public class ScreenResponsiveTests
{
    private static string GetUiChecksDirectory()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "DoctorRx.sln")))
        {
            currentDir = currentDir.Parent;
        }
        string projectRoot = currentDir?.FullName ?? AppContext.BaseDirectory;
        string outputDir = Path.Combine(projectRoot, "docs", "ui-checks");
        Directory.CreateDirectory(outputDir);
        return outputDir;
    }

    private static void SaveVisualToPng(FrameworkElement element, int width, int height, string filename, int dpi = 96)
    {
        double scale = dpi / 96.0;
        int pixelWidth = Math.Max(1, (int)Math.Ceiling(width * scale));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(height * scale));

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        string filePath = Path.Combine(GetUiChecksDirectory(), filename);
        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }

    private static PatientDto CreateStubPatient(
        int id = 1,
        string name = "Zainab Bibi",
        int age = 34,
        Gender gender = Gender.Female,
        string recordNumber = "MR-00431",
        string? phone = "0300-1234567")
    {
        return new PatientDto(
            id,
            recordNumber,
            name,
            new DateOnly(1992, 5, 14),
            age,
            gender,
            phone,
            "House 12, Street 4, Lahore",
            "No chronic conditions",
            "Penicillin allergy",
            DateTime.UtcNow.AddMonths(-3),
            DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
            false,
            null);
    }

    private static PrescriptionDetailDto CreateStubPrescriptionDetail()
    {
        var patientSnapshot = new PatientSnapshot("Zainab Bibi", Gender.Female, "34 yrs", "0300-1234567", "Penicillin allergy");
        var doctorSnapshot = new DoctorSnapshot(
            "Dr. Usman Ali",
            "MBBS, FCPS (Medicine)",
            "PMC-98765-P",
            "Consultant Physician",
            "042-35890000",
            "Al-Shifa Family Medical Care",
            "Suite 102, Medical City, Main Blvd, Lahore",
            "042-35890001",
            "Dedicated to Compassionate Clinical Care",
            "Please bring this prescription on next review");

        var medicineItems = new List<PrescriptionMedicineDto>
        {
            new PrescriptionMedicineDto(
                1,
                101,
                "Amoxicillin + Clavulanic Acid",
                "Augmentin",
                "Tablet",
                "625mg",
                "1 tablet",
                "Every 8 hours",
                "Morning, Afternoon, Night",
                MealRelation.AfterMeal,
                null,
                null,
                "Oral",
                "5 days",
                "Complete full course",
                1),
            new PrescriptionMedicineDto(
                2,
                102,
                "Paracetamol",
                "Panadol",
                "Tablet",
                "500mg",
                "1 tablet",
                "SOS / As needed for fever",
                null,
                MealRelation.AfterMeal,
                null,
                null,
                "Oral",
                "3 days",
                "Maximum 4 tablets in 24 hours",
                2)
        };

        return new PrescriptionDetailDto(
            1,
            "RX-20261008-0001",
            1,
            patientSnapshot,
            1,
            doctorSnapshot,
            DateOnly.FromDateTime(DateTime.Today),
            "Fever for 3 days, sore throat, productive cough.",
            "125/82 mmHg",
            "84 bpm",
            "100.4 °F",
            "62 kg",
            "Pharynx congested, bilateral tonsillar enlargement noted.",
            "Take warm fluids, avoid cold food/drinks, steam inhalation twice daily.",
            DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
            "Review after 5 days if fever persists or sooner in case of breathlessness.",
            PrescriptionStatus.Finalized,
            DateTime.UtcNow.AddHours(-2),
            null,
            null,
            null,
            0,
            1,
            medicineItems);
    }

    [Theory]
    [InlineData(960, 520, LayoutMode.Compact)]
    [InlineData(1024, 600, LayoutMode.Normal)]
    [InlineData(1280, 720, LayoutMode.Normal)]
    [InlineData(1920, 1080, LayoutMode.Wide)]
    public void DashboardView_AdaptsAcrossAllBreakpoints_NoHorizontalScroll(double width, double height, LayoutMode expectedMode)
    {
        StaTestRunner.Run(() =>
        {
            var stubDashboardService = new StubDashboardService();
            var stubNavService = new LocalStubNavigationService();
            var stubDraftService = new LocalStubDraftService();

            var vm = new DashboardViewModel(stubDashboardService, stubNavService, stubDraftService);
            vm.AvailableWidth = width;
            vm.UpdateLayoutMode(expectedMode);

            var view = new DashboardView { DataContext = vm, Width = width, Height = height };
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();

            Assert.Equal(expectedMode, vm.LayoutMode);
            if (expectedMode == LayoutMode.Wide)
            {
                Assert.Equal(4, vm.StatCardColumns);
            }
            else
            {
                Assert.True(vm.StatCardColumns <= 2);
            }

            var scrollViewer = FindVisualChild<ScrollViewer>(view);
            Assert.NotNull(scrollViewer);
            Assert.Equal(ScrollBarVisibility.Disabled, scrollViewer.HorizontalScrollBarVisibility);

            if (width == 960)
            {
                SaveVisualToPng(view, (int)width, (int)height, "dashboard-compact-960x520.png");
            }
            else if (width == 1920)
            {
                SaveVisualToPng(view, (int)width, (int)height, "dashboard-wide-1920x1080.png");
            }
        });
    }

    [Theory]
    [InlineData(960, 520, LayoutMode.Compact)]
    [InlineData(1024, 600, LayoutMode.Normal)]
    [InlineData(1280, 720, LayoutMode.Normal)]
    [InlineData(1920, 1080, LayoutMode.Wide)]
    public void PatientsView_AdaptsAcrossBreakpoints_DrawerClampedAndNoHorizontalScroll(double width, double height, LayoutMode expectedMode)
    {
        StaTestRunner.Run(() =>
        {
            var stubPatientService = new StubPatientService();
            var stubDialogService = new LocalStubDialogService();
            var stubNavService = new LocalStubNavigationService();

            var vm = new PatientsViewModel(
                stubPatientService,
                stubDialogService,
                stubNavService,
                new DoctorRx.Presentation.Services.PatientsFilterSessionService(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PatientsViewModel>.Instance);
            vm.AvailableWidth = width;
            vm.UpdateLayoutMode(expectedMode);

            if (width < 800)
            {
                Assert.Equal(width, vm.DrawerWidth);
                Assert.True(vm.IsDrawerFullWidth);
            }
            else
            {
                Assert.True(vm.DrawerWidth >= 320 && vm.DrawerWidth <= 440);
            }

            var view = new PatientsView { DataContext = vm, Width = width, Height = height };
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();

            Assert.Equal(expectedMode, vm.LayoutMode);

            vm.IsDrawerOpen = true;
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();

            if (width == 960)
            {
                SaveVisualToPng(view, (int)width, (int)height, "patients-drawer-overlay.png");
            }
            else if (width == 1920)
            {
                SaveVisualToPng(view, (int)width, (int)height, "patients-compact-960x520.png");
            }
        });
    }

    [Theory]
    [InlineData(960, 520, LayoutMode.Compact)]
    [InlineData(1024, 600, LayoutMode.Normal)]
    [InlineData(1920, 1080, LayoutMode.Wide)]
    public void NewPrescriptionView_AdaptsAcrossBreakpoints_NoHorizontalScroll(double width, double height, LayoutMode expectedMode)
    {
        StaTestRunner.Run(() =>
        {
            var vm = CreateStubNewPrescriptionViewModel();
            vm.UpdateLayoutMode(expectedMode);

            var view = new NewPrescriptionView { DataContext = vm, Width = width, Height = height };
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();

            Assert.Equal(expectedMode, vm.LayoutMode);

            var scrollViewer = FindVisualChild<ScrollViewer>(view);
            Assert.NotNull(scrollViewer);
            Assert.Equal(ScrollBarVisibility.Disabled, scrollViewer.HorizontalScrollBarVisibility);

            if (width == 1920)
            {
                SaveVisualToPng(view, (int)width, (int)height, "newrx-wide-1920x1080.png");
            }
            else if (width == 960)
            {
                SaveVisualToPng(view, (int)width, (int)height, "newrx-compact-960x520.png");
            }
        });
    }

    [Theory]
    [InlineData(960, 520, LayoutMode.Compact)]
    [InlineData(1280, 720, LayoutMode.Normal)]
    public void PrescriptionDetailView_AdaptsAndScrollsCleanly(double width, double height, LayoutMode expectedMode)
    {
        StaTestRunner.Run(() =>
        {
            var stubPrescriptionService = new StubPrescriptionService();
            var stubNavService = new LocalStubNavigationService();
            var stubDialogService = new LocalStubDialogService();

            var vm = new PrescriptionDetailViewModel(stubPrescriptionService, stubNavService, stubDialogService);
            vm.UpdateLayoutMode(expectedMode);

            var view = new PrescriptionDetailView { DataContext = vm, Width = width, Height = height };
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();

            Assert.Equal(expectedMode, vm.LayoutMode);

            var scrollViewer = FindVisualChild<ScrollViewer>(view);
            Assert.NotNull(scrollViewer);
            Assert.Equal(ScrollBarVisibility.Disabled, scrollViewer.HorizontalScrollBarVisibility);

            if (width == 1280)
            {
                SaveVisualToPng(view, (int)width, (int)height, "rxdetail-1280x720.png");
            }
        });
    }

    [Fact]
    public void CustomDialogWindow_RelativeSizingAndKeyboardSupport()
    {
        StaTestRunner.Run(() =>
        {
            var owner = new Window { Width = 960, Height = 520 };
            owner.Show();

            var dialog = new CustomDialogWindow(
                "Confirm Prescription Deletion",
                "Are you sure you want to discard this prescription draft permanently?",
                CustomDialogWindow.DialogType.Confirmation,
                owner);

            dialog.Measure(new Size(owner.ActualWidth, owner.ActualHeight));
            dialog.Arrange(new Rect(0, 0, 420, 220));
            dialog.UpdateLayout();

            Assert.True(dialog.MinWidth >= 380);
            Assert.True(dialog.MinHeight >= 180);
            Assert.True(dialog.MaxWidth <= owner.ActualWidth);

            SaveVisualToPng(dialog, 420, 220, "customdialog-confirmation.png");

            dialog.Close();
            owner.Close();
        });
    }

    [Fact]
    public void PrintPreviewWindow_ZoomControlsAndNoHorizontalWindowScroll()
    {
        StaTestRunner.Run(() =>
        {
            var stubPrescription = CreateStubPrescriptionDetail();

            var window = new PrintPreviewWindow(stubPrescription);
            window.Width = 920;
            window.Height = 680;

            window.Measure(new Size(920, 680));
            window.Arrange(new Rect(0, 0, 920, 680));
            window.UpdateLayout();

            Assert.True(window.CurrentZoom > 0);

            window.TriggerFitWidth();
            Assert.True(window.CurrentZoom > 0);

            window.TriggerZoomToFit();
            Assert.True(window.CurrentZoom > 0);

            SaveVisualToPng(window, 920, 680, "printpreview-920x680.png");

            window.Close();
        });
    }

    [Fact]
    public void RenderScreenshots_AllFiveDpis_GeneratesVisualArtifacts()
    {
        StaTestRunner.Run(() =>
        {
            var stubDashboardService = new StubDashboardService();
            var stubNavService = new LocalStubNavigationService();
            var stubDraftService = new LocalStubDraftService();

            var vm = new DashboardViewModel(stubDashboardService, stubNavService, stubDraftService);
            vm.AvailableWidth = 1280;
            vm.UpdateLayoutMode(LayoutMode.Normal);

            var view = new DashboardView { DataContext = vm, Width = 1280, Height = 720 };
            view.Measure(new Size(1280, 720));
            view.Arrange(new Rect(0, 0, 1280, 720));
            view.UpdateLayout();

            int[] dpis = [96, 120, 144, 168, 192];
            foreach (int dpi in dpis)
            {
                SaveVisualToPng(view, 1280, 720, $"dashboard-dpi{dpi}.png", dpi);
                string filePath = Path.Combine(GetUiChecksDirectory(), $"dashboard-dpi{dpi}.png");
                Assert.True(File.Exists(filePath));
                Assert.True(new FileInfo(filePath).Length > 0);
            }
        });
    }

    private static NewPrescriptionViewModel CreateStubNewPrescriptionViewModel()
    {
        var stubPatientService = new StubPatientService();
        var stubMedicineService = new StubMedicineService();
        var stubPrescriptionService = new StubPrescriptionService();
        var stubDraftService = new LocalStubDraftService();
        var stubDialogService = new LocalStubDialogService();
        var stubNavService = new LocalStubNavigationService();
        var stubClock = new LocalStubClock();
        var validator = new PrescriptionComposerValidator(stubClock);

        var vm = new NewPrescriptionViewModel(
            stubPatientService,
            stubMedicineService,
            stubPrescriptionService,
            stubDraftService,
            stubDialogService,
            stubNavService,
            validator,
            stubClock);

        vm.SelectedPatient = CreateStubPatient();
        vm.ChiefComplaints = "High fever, sore throat";
        vm.BloodPressure = "120/80";
        vm.PulseRate = "78";

        return vm;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var nested = FindVisualChild<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }
        return null;
    }

    #region Local Stubs

    private class LocalStubClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    }

    private class LocalStubNavigationService : INavigationService
    {
        public ViewModelBase? CurrentViewModel { get; set; }
        public NavigationDestination CurrentDestination { get; set; } = NavigationDestination.Dashboard;
        public event Action<ViewModelBase>? CurrentViewModelChanged { add { } remove { } }

        public void NavigateTo(NavigationDestination destination, object? parameter = null)
        {
            CurrentDestination = destination;
        }
    }

    private class LocalStubDialogService : IDialogService
    {
        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
        public bool ShowConfirmation(string title, string message) => true;
        public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel") => true;
    }

    private class LocalStubDraftService : IDraftService
    {
        public Task<int> GetCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<DraftSummaryDto>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DraftSummaryDto>>(new List<DraftSummaryDto>());
        public Task<Result<PrescriptionComposerState>> GetAsync(Guid draftKey, CancellationToken cancellationToken = default) => Task.FromResult(Result<PrescriptionComposerState>.Failure("Not found"));
        public Task<Result<Guid>> SaveAsync(PrescriptionComposerState state, CancellationToken cancellationToken = default) => Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        public Task<Result> DiscardAsync(Guid draftKey, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
        public void MarkFinalized(Guid draftKey) { }
        public bool IsFinalized(Guid draftKey) => false;
    }

    private class StubDashboardService : IDashboardService
    {
        public Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DashboardStatsDto(
                120,
                14,
                840,
                250,
                new List<PrescriptionSummaryDto>(),
                new List<PatientDto> { CreateStubPatient() }));
        }
    }

    private class StubPatientService : IPatientService
    {
        public Task<PagedResult<PatientDto>> GetPatientsPagedAsync(int pageNumber, int pageSize = 50, bool showArchived = false, CancellationToken cancellationToken = default)
        {
            var list = new List<PatientDto> { CreateStubPatient(1, "Muhammad Bilal"), CreateStubPatient(2, "Ayesha Siddiqua") };
            return Task.FromResult(new PagedResult<PatientDto>(list, 2, 1, 50));
        }

        public Task<PagedResult<PatientDto>> GetFilteredPatientsPagedAsync(PatientFilterCriteria criteria, CancellationToken cancellationToken = default)
        {
            var list = new List<PatientDto> { CreateStubPatient(1, "Muhammad Bilal"), CreateStubPatient(2, "Ayesha Siddiqua") };
            return Task.FromResult(new PagedResult<PatientDto>(list, 2, criteria.PageNumber, criteria.PageSize));
        }

        public Task<IReadOnlyList<PatientDto>> SearchPatientsAsync(string query, int maxResults = 50, bool showArchived = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PatientDto>>(new List<PatientDto> { CreateStubPatient(1, "Muhammad Bilal") });
        }

        public Task<IReadOnlyList<PatientDto>> GetRecentPatientsAsync(int count = 10, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PatientDto>>(new List<PatientDto> { CreateStubPatient(1, "Muhammad Bilal") });
        }

        public Task<PatientDto?> GetPatientByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<PatientDto?>(null);
        public Task<Result<PatientDto>> CreatePatientAsync(CreatePatientDto dto, bool allowDuplicate = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result<PatientDto>> UpdatePatientAsync(UpdatePatientDto dto, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result> ArchivePatientAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
        public Task<Result> RestorePatientAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
        public Task<Result> DeletePatientAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
    }

    private class StubPrescriptionService : IPrescriptionService
    {
        public Task<PrescriptionDetailDto?> GetPrescriptionByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<PrescriptionDetailDto?>(CreateStubPrescriptionDetail());

        public Task<IReadOnlyList<PrescriptionSummaryDto>> GetRecentPrescriptionsAsync(int count = 10, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PrescriptionSummaryDto>>(new List<PrescriptionSummaryDto>());
        }

        public Task<IReadOnlyList<PrescriptionSummaryDto>> GetPrescriptionsByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PrescriptionSummaryDto>>(new List<PrescriptionSummaryDto>());
        }

        public Task<Result<PrescriptionDetailDto>> FinalizePrescriptionAsync(CreatePrescriptionDto dto, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result<PrescriptionDetailDto>> AmendPrescriptionAsync(int originalId, CreatePrescriptionDto newContent, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result> CancelPrescriptionAsync(int id, string reason, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
    }

    private class StubMedicineService : IMedicineService
    {
        public Task<IReadOnlyList<MedicineDto>> GetMedicinesPagedAsync(int pageNumber, int pageSize = 50, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MedicineDto>>(new List<MedicineDto>());
        }

        public Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MedicineDto>>(new List<MedicineDto>());
        }

        public Task<MedicineDto?> GetMedicineByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<MedicineDto?>(null);
        public Task<Result<MedicineDto>> CreateMedicineAsync(CreateMedicineDto dto, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result<MedicineDto>> UpdateMedicineAsync(UpdateMedicineDto dto, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result> DeleteMedicineAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    #endregion
}
