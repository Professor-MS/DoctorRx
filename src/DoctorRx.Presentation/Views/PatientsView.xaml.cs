using System.Windows;
using System.Windows.Controls;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;

namespace DoctorRx.Presentation.Views;

public partial class PatientsView : UserControl
{
    public PatientsView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is PatientsViewModel vm && e.NewSize.Width > 0)
        {
            vm.AvailableWidth = e.NewSize.Width;
            vm.UpdateLayoutMode(LayoutBreakpoints.DetermineMode(e.NewSize.Width));
        }
    }
}
