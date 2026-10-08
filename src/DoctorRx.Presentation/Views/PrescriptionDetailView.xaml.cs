using System.Windows.Controls;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;

namespace DoctorRx.Presentation.Views;

public partial class PrescriptionDetailView : UserControl
{
    public PrescriptionDetailView()
    {
        InitializeComponent();
        SizeChanged += (s, e) =>
        {
            if (DataContext is PrescriptionDetailViewModel vm && e.NewSize.Width > 0)
            {
                vm.UpdateLayoutMode(LayoutBreakpoints.DetermineMode(e.NewSize.Width));
            }
        };
    }
}
