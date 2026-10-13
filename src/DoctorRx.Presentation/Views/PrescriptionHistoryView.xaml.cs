using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DoctorRx.Presentation.ViewModels;

namespace DoctorRx.Presentation.Views;

public partial class PrescriptionHistoryView : UserControl
{
    private ScrollViewer? _gridScrollViewer;

    public PrescriptionHistoryView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_gridScrollViewer == null)
        {
            _gridScrollViewer = FindVisualChild<ScrollViewer>(PrescriptionsGrid);
            if (_gridScrollViewer != null)
            {
                _gridScrollViewer.ScrollChanged += OnGridScrollChanged;
            }
        }

        if (DataContext is PrescriptionHistoryViewModel vm && _gridScrollViewer != null)
        {
            if (vm.ScrollPosition > 0)
            {
                _gridScrollViewer.ScrollToVerticalOffset(vm.ScrollPosition);
            }

            if (vm.SelectedPrescription != null)
            {
                PrescriptionsGrid.ScrollIntoView(vm.SelectedPrescription);
            }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_gridScrollViewer != null)
        {
            _gridScrollViewer.ScrollChanged -= OnGridScrollChanged;
            _gridScrollViewer = null;
        }
    }

    private void OnGridScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (DataContext is PrescriptionHistoryViewModel vm && e.VerticalChange != 0)
        {
            vm.ScrollPosition = e.VerticalOffset;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
            {
                return descendant;
            }
        }
        return null;
    }
}
