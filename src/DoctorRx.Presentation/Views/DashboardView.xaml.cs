using System.Windows;
using System.Windows.Controls;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;

namespace DoctorRx.Presentation.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm && e.NewSize.Width > 0)
        {
            vm.AvailableWidth = e.NewSize.Width;
            vm.UpdateLayoutMode(LayoutBreakpoints.DetermineMode(e.NewSize.Width));
        }
    }
}
