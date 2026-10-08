using System.ComponentModel;
using System.Windows;
using DoctorRx.Presentation.ViewModels;

namespace DoctorRx.Presentation;

public partial class MainWindow : Window
{
    private bool _isExplicitlyClosing;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (s, e) =>
        {
            await viewModel.InitializeAsync();
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
        }
    }
}