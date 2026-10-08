using System.Windows.Controls;
using System.Windows.Input;
using DoctorRx.Presentation.ViewModels;

namespace DoctorRx.Presentation.Views;

public partial class NewPrescriptionView : UserControl
{
    public NewPrescriptionView()
    {
        InitializeComponent();
        SizeChanged += (s, e) =>
        {
            if (DataContext is NewPrescriptionViewModel vm && e.NewSize.Width > 0)
            {
                vm.UpdateLayoutMode(Services.LayoutBreakpoints.DetermineMode(e.NewSize.Width));
            }
        };
    }

    private void OnMedicineEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not NewPrescriptionViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            // Enter adds the medicine and refocuses Medicine name
            if (vm.AddOrUpdateMedicineCommand.CanExecute(null))
            {
                vm.AddOrUpdateMedicineCommand.Execute(null);
                e.Handled = true;
                MedicineSearchBox.Focus();
                MedicineSearchBox.SelectAll();
            }
        }
        else if (e.Key == Key.Escape)
        {
            // Esc cancels the editor and refocuses Medicine name
            if (vm.CancelEditMedicineCommand.CanExecute(null))
            {
                vm.CancelEditMedicineCommand.Execute(null);
                e.Handled = true;
                MedicineSearchBox.Focus();
            }
        }
    }
}
