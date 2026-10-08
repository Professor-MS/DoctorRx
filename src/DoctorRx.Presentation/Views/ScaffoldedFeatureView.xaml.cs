using System.Windows.Controls;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;

namespace DoctorRx.Presentation.Views;

public partial class ScaffoldedFeatureView : UserControl
{
    public ScaffoldedFeatureView()
    {
        InitializeComponent();
        SizeChanged += (s, e) =>
        {
            if (DataContext is ViewModelBase vm && e.NewSize.Width > 0)
            {
                vm.UpdateLayoutMode(LayoutBreakpoints.DetermineMode(e.NewSize.Width));
            }
        };
    }
}
