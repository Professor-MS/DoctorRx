using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DoctorRx.Presentation.Views;

public partial class CustomDialogWindow : Window
{
    public bool? DialogBooleanResult { get; private set; }

    public enum DialogType
    {
        Information,
        Warning,
        Error,
        Confirmation,
        ConfirmationWithCancel
    }

    public CustomDialogWindow(string title, string message, DialogType type, Window? owner = null)
    {
        InitializeComponent();

        Window? potentialOwner = owner;
        if (potentialOwner == null && System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            potentialOwner = System.Windows.Application.Current.MainWindow;
        }

        if (potentialOwner != null && potentialOwner.Dispatcher.CheckAccess() && potentialOwner.IsVisible)
        {
            try
            {
                Owner = potentialOwner;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                MaxWidth = Math.Max(380, potentialOwner.ActualWidth * 0.9);
                MaxHeight = Math.Max(240, potentialOwner.ActualHeight * 0.85);
            }
            catch
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                MaxWidth = 520;
                MaxHeight = 400;
            }
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            MaxWidth = 520;
            MaxHeight = 400;
            Topmost = true;
        }

        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;

        ConfigureDialogType(type);

        Loaded += (s, e) =>
        {
            Activate();
            Focus();
        };
    }

    private void ConfigureDialogType(DialogType type)
    {
        switch (type)
        {
            case DialogType.Information:
                IconPath.Data = (Geometry)FindResource("IconInfo");
                IconPath.Fill = (Brush)FindResource("PrimaryTealDarkBrush");
                IconBadge.Background = (Brush)FindResource("PrimaryTealUltraLightBrush");
                PrimaryButton.Content = "OK";
                SecondaryButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
                break;

            case DialogType.Warning:
                IconPath.Data = (Geometry)FindResource("IconWarning");
                IconPath.Fill = new SolidColorBrush(Color.FromRgb(217, 119, 6)); // Amber-600
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // Amber-100
                PrimaryButton.Content = "OK";
                SecondaryButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
                break;

            case DialogType.Error:
                IconPath.Data = (Geometry)FindResource("IconWarning");
                IconPath.Fill = (Brush)FindResource("DangerRedBrush");
                IconBadge.Background = (Brush)FindResource("DangerRedLightBrush");
                PrimaryButton.Content = "OK";
                SecondaryButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
                break;

            case DialogType.Confirmation:
                IconPath.Data = (Geometry)FindResource("IconInfo");
                IconPath.Fill = (Brush)FindResource("PrimaryTealDarkBrush");
                IconBadge.Background = (Brush)FindResource("PrimaryTealUltraLightBrush");
                PrimaryButton.Content = "Yes";
                SecondaryButton.Content = "No";
                SecondaryButton.Visibility = Visibility.Visible;
                SecondaryButton.IsCancel = true; // Esc triggers No
                CancelButton.Visibility = Visibility.Collapsed;
                break;

            case DialogType.ConfirmationWithCancel:
                IconPath.Data = (Geometry)FindResource("IconInfo");
                IconPath.Fill = (Brush)FindResource("PrimaryTealDarkBrush");
                IconBadge.Background = (Brush)FindResource("PrimaryTealUltraLightBrush");
                PrimaryButton.Content = "Yes";
                SecondaryButton.Content = "No";
                SecondaryButton.Visibility = Visibility.Visible;
                SecondaryButton.IsCancel = false;
                CancelButton.Content = "Cancel";
                CancelButton.Visibility = Visibility.Visible;
                CancelButton.IsCancel = true; // Esc triggers Cancel
                break;
        }
    }

    private void OnPrimaryClicked(object sender, RoutedEventArgs e)
    {
        DialogBooleanResult = true;
        DialogResult = true;
        Close();
    }

    private void OnSecondaryClicked(object sender, RoutedEventArgs e)
    {
        DialogBooleanResult = false;
        DialogResult = false;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogBooleanResult = null;
        DialogResult = false;
        Close();
    }

    public static void ShowError(Window? owner, string message, string title = "Error")
    {
        var dialog = new CustomDialogWindow(title, message, DialogType.Error, owner);
        dialog.ShowDialog();
    }

    public static void ShowInfo(Window? owner, string message, string title = "Information")
    {
        var dialog = new CustomDialogWindow(title, message, DialogType.Information, owner);
        dialog.ShowDialog();
    }

    public static void ShowWarning(Window? owner, string message, string title = "Warning")
    {
        var dialog = new CustomDialogWindow(title, message, DialogType.Warning, owner);
        dialog.ShowDialog();
    }

    public static bool ShowConfirmation(Window? owner, string message, string title = "Confirmation")
    {
        var dialog = new CustomDialogWindow(title, message, DialogType.Confirmation, owner);
        dialog.ShowDialog();
        return dialog.DialogBooleanResult == true;
    }
}
