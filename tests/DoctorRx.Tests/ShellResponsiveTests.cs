using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Entities;
using DoctorRx.Presentation;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using Xunit;

namespace DoctorRx.Tests;

public class ShellResponsiveTests
{
    [Theory]
    [InlineData(960, 520, LayoutMode.Compact, 64)]
    [InlineData(1024, 600, LayoutMode.Normal, 240)]
    [InlineData(1280, 720, LayoutMode.Normal, 240)]
    [InlineData(1920, 1080, LayoutMode.Wide, 240)]
    public void MainWindow_AtSpecifiedDimensions_MatchesLayoutModeAndSidebarWidth(
        double width, double height, LayoutMode expectedMode, double expectedSidebarWidth)
    {
        StaTestRunner.Run(() =>
        {
            var navService = new StubNavigationService();
            var docService = new StubDoctorService();
            var dialogService = new StubDialogService();
            var draftService = new StubDraftService();
            var placementService = new StubWindowPlacementService();

            var viewModel = new MainWindowViewModel(navService, docService, dialogService, draftService, placementService);
            var window = new MainWindow(viewModel, placementService);

            // Sizing window and forcing layout pass
            window.Width = width;
            window.Height = height;
            viewModel.UpdateLayoutWidth(width);

            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));
            window.UpdateLayout();

            // 1. Assert Layout Mode matches width
            Assert.Equal(expectedMode, viewModel.LayoutMode);

            // 2. Assert Sidebar mode (width) matches width
            Assert.Equal(expectedSidebarWidth, viewModel.SidebarWidth);
            if (expectedMode == LayoutMode.Compact)
            {
                Assert.True(viewModel.IsSidebarCollapsed);
                Assert.True(viewModel.IsCompact);
            }
            else
            {
                Assert.False(viewModel.IsSidebarCollapsed);
                Assert.False(viewModel.IsCompact);
            }

            // 3. Assert Key Controls are inside Window Bounds
            var contentRoot = (FrameworkElement)window.Content;
            contentRoot.Width = width;
            contentRoot.Height = height;
            contentRoot.Measure(new Size(width, height));
            contentRoot.Arrange(new Rect(0, 0, width, height));
            contentRoot.UpdateLayout();

            Assert.True(contentRoot.ActualWidth <= width);
            Assert.True(contentRoot.ActualHeight <= height);

            // Find ScrollViewer content host and verify horizontal scroll is disabled
            var scrollViewer = FindVisualChild<ScrollViewer>(contentRoot);
            Assert.NotNull(scrollViewer);
            Assert.Equal(ScrollBarVisibility.Disabled, scrollViewer.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);

            window.Close();
        });
    }

    [Fact]
    public void MainWindow_HamburgerToggle_OverridesSidebarModeAndRemembersChoice()
    {
        StaTestRunner.Run(() =>
        {
            var navService = new StubNavigationService();
            var docService = new StubDoctorService();
            var dialogService = new StubDialogService();
            var draftService = new StubDraftService();
            var placementService = new StubWindowPlacementService();

            var viewModel = new MainWindowViewModel(navService, docService, dialogService, draftService, placementService);
            var window = new MainWindow(viewModel, placementService);

            // At 1280 DIPs (Normal mode), default sidebar is expanded (240 DIPs)
            viewModel.UpdateLayoutWidth(1280);
            Assert.False(viewModel.IsSidebarCollapsed);
            Assert.Equal(LayoutBreakpoints.SidebarExpandedWidth, viewModel.SidebarWidth);

            // Toggle hamburger button
            viewModel.ToggleSidebarCommand.Execute(null);
            Assert.True(viewModel.IsSidebarCollapsed);
            Assert.True(viewModel.HasExplicitSidebarOverride);
            Assert.Equal(LayoutBreakpoints.SidebarCollapsedWidth, viewModel.SidebarWidth);
            Assert.True(placementService.SavedSettings?.IsSidebarCollapsedOverride);

            // Toggle again to expand
            viewModel.ToggleSidebarCommand.Execute(null);
            Assert.False(viewModel.IsSidebarCollapsed);
            Assert.False(placementService.SavedSettings?.IsSidebarCollapsedOverride);

            window.Close();
        });
    }

    [Fact]
    public void WindowPlacementService_OffScreenCoordinates_IdentifiedCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DoctorRx_PlacementTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var appPaths = new StubAppPaths(tempDir);
        var placementService = new WindowPlacementService(appPaths);

        // Coordinates far outside any possible screen (e.g. -50000, -50000)
        bool isOnScreenNegative = placementService.IsOnScreen(-50000, -50000, 1000, 600);
        bool isOnScreenHuge = placementService.IsOnScreen(999999, 999999, 1000, 600);
        bool isOnScreenInvalidZero = placementService.IsOnScreen(0, 0, 10, 10);

        Assert.False(isOnScreenNegative);
        Assert.False(isOnScreenHuge);
        Assert.False(isOnScreenInvalidZero);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void WindowPlacementService_SavesAndRestoresPlacementAccurately()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DoctorRx_PlacementSave_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var appPaths = new StubAppPaths(tempDir);
        var placementService = new WindowPlacementService(appPaths);

        var settings = new WindowPlacementSettings
        {
            Left = 120,
            Top = 140,
            Width = 1100,
            Height = 650,
            IsMaximized = false,
            IsSidebarCollapsedOverride = true
        };

        placementService.SavePlacement(settings);
        var loaded = placementService.LoadPlacement();

        Assert.NotNull(loaded);
        Assert.Equal(120, loaded.Left);
        Assert.Equal(140, loaded.Top);
        Assert.Equal(1100, loaded.Width);
        Assert.Equal(650, loaded.Height);
        Assert.False(loaded.IsMaximized);
        Assert.True(loaded.IsSidebarCollapsedOverride);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void RenderShellScreenshots_AtAllStandardDpis_GeneratesVisualArtifacts()
    {
        StaTestRunner.Run(() =>
        {
            var navService = new StubNavigationService();
            var docService = new StubDoctorService();
            var dialogService = new StubDialogService();
            var draftService = new StubDraftService();
            var placementService = new StubWindowPlacementService();

            var viewModel = new MainWindowViewModel(navService, docService, dialogService, draftService, placementService);
            var window = new MainWindow(viewModel, placementService);

            // Base size: 1024 x 600
            double width = 1024;
            double height = 600;
            viewModel.UpdateLayoutWidth(width);

            var contentRoot = (FrameworkElement)window.Content;
            contentRoot.Width = width;
            contentRoot.Height = height;
            contentRoot.Measure(new Size(width, height));
            contentRoot.Arrange(new Rect(0, 0, width, height));
            contentRoot.UpdateLayout();

            // Output directory: docs/ui-checks/
            var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
            while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "DoctorRx.sln")))
            {
                currentDir = currentDir.Parent;
            }
            string projectRoot = currentDir?.FullName ?? AppContext.BaseDirectory;
            string outputDir = Path.Combine(projectRoot, "docs", "ui-checks");
            Directory.CreateDirectory(outputDir);

            int[] dpis = [96, 120, 144, 168, 192];

            foreach (int dpi in dpis)
            {
                double scale = dpi / 96.0;
                int pixelWidth = (int)Math.Ceiling(width * scale);
                int pixelHeight = (int)Math.Ceiling(height * scale);

                var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
                rtb.Render(contentRoot);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                string filePath = Path.Combine(outputDir, $"shell-dpi{dpi}.png");
                using var stream = File.Create(filePath);
                encoder.Save(stream);

                Assert.True(File.Exists(filePath));
                Assert.True(new FileInfo(filePath).Length > 0);
            }

            window.Close();
        });
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                return typed;
            }

            var result = FindVisualChild<T>(child);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    #region Stubs

    private class StubAppPaths : IAppPaths
    {
        public string BaseDirectory { get; }
        public string DataDirectory => Path.Combine(BaseDirectory, "Data");
        public string DatabasePath => Path.Combine(DataDirectory, "doctorrx.db");
        public string BackupsDirectory => Path.Combine(BaseDirectory, "Backups");
        public string DraftsDirectory => Path.Combine(BaseDirectory, "Drafts");
        public string LogsDirectory => Path.Combine(BaseDirectory, "Logs");
        public string AssetsDirectory => Path.Combine(BaseDirectory, "Assets");

        public StubAppPaths(string baseDir) => BaseDirectory = baseDir;
    }

    private class StubWindowPlacementService : IWindowPlacementService
    {
        public WindowPlacementSettings? SavedSettings { get; set; }

        public WindowPlacementSettings? LoadPlacement() => SavedSettings;

        public void SavePlacement(WindowPlacementSettings settings) => SavedSettings = settings;

        public void ApplyPlacement(Window window)
        {
            window.Width = LayoutBreakpoints.ShellMinWidth;
            window.Height = LayoutBreakpoints.ShellMinHeight;
        }

        public void PersistPlacement(Window window, bool? isSidebarCollapsedOverride = null)
        {
            SavedSettings = new WindowPlacementSettings
            {
                Width = window.ActualWidth,
                Height = window.ActualHeight,
                IsSidebarCollapsedOverride = isSidebarCollapsedOverride
            };
        }

        public bool IsOnScreen(double left, double top, double width, double height) => true;
    }

    private class StubNavigationService : INavigationService
    {
        public ViewModelBase? CurrentViewModel { get; set; }
        public NavigationDestination CurrentDestination { get; set; } = NavigationDestination.Dashboard;
        public event Action<ViewModelBase>? CurrentViewModelChanged { add { } remove { } }

        public void NavigateTo(NavigationDestination destination, object? parameter = null)
        {
            CurrentDestination = destination;
        }
    }

    private class StubDoctorService : IDoctorService
    {
        public Task<DoctorDto?> GetActiveDoctorAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DoctorDto?>(new DoctorDto(
                1,
                "Dr. Test Physician",
                "MBBS",
                "12345",
                "Cardiologist",
                null,
                null,
                "Test Clinic",
                null,
                null,
                null,
                null
            ));

        public Task<DoctorRx.Application.Common.Result<DoctorDto>> CreateDoctorAsync(CreateDoctorDto dto, CancellationToken cancellationToken = default) =>
            Task.FromResult(DoctorRx.Application.Common.Result<DoctorDto>.Success(new DoctorDto(
                1, dto.Name, dto.Qualification, dto.RegistrationNumber, dto.Specialization,
                dto.Phone, dto.Email, dto.ClinicName, dto.ClinicAddress, dto.ClinicPhone, dto.HeaderText, dto.FooterText)));

        public Task<DoctorRx.Application.Common.Result<DoctorDto>> UpdateDoctorAsync(UpdateDoctorDto dto, CancellationToken cancellationToken = default) =>
            Task.FromResult(DoctorRx.Application.Common.Result<DoctorDto>.Success(new DoctorDto(
                dto.Id, dto.Name, dto.Qualification, dto.RegistrationNumber, dto.Specialization,
                dto.Phone, dto.Email, dto.ClinicName, dto.ClinicAddress, dto.ClinicPhone, dto.HeaderText, dto.FooterText)));
    }

    private class StubDialogService : IDialogService
    {
        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
        public bool ShowConfirmation(string title, string message) => true;
        public bool? ShowConfirmationWithCancel(string title, string message) => true;
    }

    private class StubDraftService : IDraftService
    {
        public Task<int> GetCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<DraftSummaryDto>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DraftSummaryDto>>(new List<DraftSummaryDto>());
        public Task<DoctorRx.Application.Common.Result<PrescriptionComposerState>> GetAsync(Guid draftKey, CancellationToken cancellationToken = default) => Task.FromResult(DoctorRx.Application.Common.Result<PrescriptionComposerState>.Failure("Not found"));
        public Task<DoctorRx.Application.Common.Result<Guid>> SaveAsync(PrescriptionComposerState state, CancellationToken cancellationToken = default) => Task.FromResult(DoctorRx.Application.Common.Result<Guid>.Success(Guid.NewGuid()));
        public Task<DoctorRx.Application.Common.Result> DiscardAsync(Guid draftKey, CancellationToken cancellationToken = default) => Task.FromResult(DoctorRx.Application.Common.Result.Success());
        public void MarkFinalized(Guid draftKey) { }
        public bool IsFinalized(Guid draftKey) => false;
    }

    #endregion
}
