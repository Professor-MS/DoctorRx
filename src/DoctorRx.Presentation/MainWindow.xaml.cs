using System.Windows;
using DoctorRx.Presentation.ViewModels;

namespace DoctorRx.Presentation;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (s, e) =>
        {
            await viewModel.InitializeAsync();
        };
    }
}