using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DoctorRx.Presentation.ViewModels;

namespace DoctorRx.Presentation.Views;

public partial class NewPrescriptionView : UserControl
{
    private NewPrescriptionViewModel? _currentViewModel;

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

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_currentViewModel != null)
        {
            _currentViewModel.FocusRequested -= OnFocusRequested;
            _currentViewModel.ScrollLastMedicineIntoViewRequested -= OnScrollLastMedicineIntoViewRequested;
        }

        if (e.NewValue is NewPrescriptionViewModel newVm)
        {
            _currentViewModel = newVm;
            _currentViewModel.FocusRequested += OnFocusRequested;
            _currentViewModel.ScrollLastMedicineIntoViewRequested += OnScrollLastMedicineIntoViewRequested;
        }
        else
        {
            _currentViewModel = null;
        }
    }

    private void OnFocusRequested(string target)
    {
        Dispatcher.BeginInvoke(() =>
        {
            switch (target)
            {
                case "MedicineName":
                    MedicineSearchBox?.Focus();
                    MedicineSearchBox?.SelectAll();
                    break;
                case "Dose":
                    DoseTextBox?.Focus();
                    DoseTextBox?.SelectAll();
                    break;
                case "Frequency":
                    FrequencyTextBox?.Focus();
                    FrequencyTextBox?.SelectAll();
                    break;
            }
        });
    }

    private void OnScrollLastMedicineIntoViewRequested()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (PrescribedMedicinesControl != null && PrescribedMedicinesControl.Items.Count > 0)
            {
                var lastItem = PrescribedMedicinesControl.Items[^1];
                if (PrescribedMedicinesControl.ItemContainerGenerator.ContainerFromItem(lastItem) is FrameworkElement container)
                {
                    container.BringIntoView();
                }
                else
                {
                    PrescribedMedicinesControl.BringIntoView();
                }
            }
        });
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
