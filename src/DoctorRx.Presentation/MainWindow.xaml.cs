using System.ComponentModel;
using System.Windows;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;

namespace DoctorRx.Presentation;

public partial class MainWindow : Window
{
    private readonly IWindowPlacementService? _placementService;
    private bool _isExplicitlyClosing;

    public MainWindow(MainWindowViewModel viewModel, IWindowPlacementService? placementService = null)
    {
        _placementService = placementService;
        InitializeComponent();
        DataContext = viewModel;

        // Apply saved placement or first-run bounds
        _placementService?.ApplyPlacement(this);
        viewModel.UpdateLayoutWidth(Width > 0 ? Width : ActualWidth);

        SizeChanged += (s, e) =>
        {
            viewModel.UpdateLayoutWidth(e.NewSize.Width);
        };

        DpiChanged += (s, e) =>
        {
            // PerMonitorV2: Ensure layout adjusts smoothly when moved across monitors with different DPI scaling
            UpdateLayout();
        };

        Loaded += (s, e) =>
        {
            Serilog.Log.Information("MainWindow Loaded event fired. ActualWidth: {Width}, ActualHeight: {Height}", ActualWidth, ActualHeight);
            viewModel.UpdateLayoutWidth(ActualWidth);
            Activate();
            Focus();
        };

        ContentRendered += (s, e) =>
        {
            Serilog.Log.Information("MainWindow ContentRendered event fired. Window is fully visible and rendered.");
            Dispatcher.InvokeAsync(async () =>
            {
                await viewModel.InitializeAsync();
                Serilog.Log.Information("MainWindow ViewModel initialized successfully.");
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        };

        Closing += OnClosing;
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isExplicitlyClosing)
        {
            return;
        }

        if (DataContext is MainWindowViewModel mainVm && mainVm.CurrentView is NewPrescriptionViewModel newRxVm && newRxVm.HasUnsavedChanges)
        {
            // Amendment 5: cancel the close, await the draft save, then close again. Never block UI with .Result/.Wait.
            e.Cancel = true;

            var decision = mainVm.ConfirmCloseWithUnsavedChanges();
            if (decision == null)
            {
                // User cancelled: keep window open and stay on the current screen
                return;
            }

            if (decision == true)
            {
                // Save draft asynchronously without blocking UI thread
                await newRxVm.SaveDraftInternalAsync(explicitUserSave: false);
            }

            _isExplicitlyClosing = true;
            Close();
            return;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            _placementService?.PersistPlacement(this, vm.HasExplicitSidebarOverride ? vm.IsSidebarCollapsed : null);
        }
    }
}