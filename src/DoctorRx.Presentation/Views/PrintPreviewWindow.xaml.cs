using System;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Presentation.Views;

public partial class PrintPreviewWindow : Window
{
    private double _currentZoom = 1.0;
    private PrescriptionDetailDto? _prescription;

    // Standard paper sizes at 96 DPI:
    // A4: 210mm x 297mm = 8.27in x 11.69in = 793.7 x 1122.5 WPF units
    // A5: 148mm x 210mm = 5.83in x 8.27in = 559.4 x 793.7 WPF units
    public const double A4Width = 793.7;
    public const double A4Height = 1122.5;
    public const double A5Width = 559.4;
    public const double A5Height = 793.7;

    public bool IsA5Selected => PaperSizeSelector?.SelectedIndex == 1;
    public Border PaperContainerElement => PaperContainer;
    public ComboBox PaperSizeSelectorElement => PaperSizeSelector;

    public PrintPreviewWindow(PrescriptionDetailDto? prescription, Window? owner = null)
    {
        InitializeComponent();
        _prescription = prescription;

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
                MaxWidth = Math.Max(700, potentialOwner.ActualWidth * 0.95);
                MaxHeight = Math.Max(500, potentialOwner.ActualHeight * 0.95);
            }
            catch
            {
                // Fallback to center screen if owner cannot be assigned
            }
        }

        if (prescription != null)
        {
            PaperView.Populate(prescription);
        }

        Loaded += (s, e) => FitWidth();
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    private void SetZoom(double zoom)
    {
        _currentZoom = Math.Clamp(zoom, 0.4, 2.5);
        DocumentScaleTransform.ScaleX = _currentZoom;
        DocumentScaleTransform.ScaleY = _currentZoom;
    }

    private void FitWidth()
    {
        if (DocumentScrollViewer.ActualWidth > 80 && PaperContainer.Width > 0)
        {
            var targetScale = (DocumentScrollViewer.ActualWidth - 64) / PaperContainer.Width;
            SetZoom(targetScale);
        }
    }

    private void ZoomToFit()
    {
        if (DocumentScrollViewer.ActualWidth > 80 && DocumentScrollViewer.ActualHeight > 80 && PaperContainer.Height > 0)
        {
            var scaleX = (DocumentScrollViewer.ActualWidth - 64) / PaperContainer.Width;
            var scaleY = (DocumentScrollViewer.ActualHeight - 64) / PaperContainer.Height;
            SetZoom(Math.Min(scaleX, scaleY));
        }
    }

    public double CurrentZoom => _currentZoom;
    public void TriggerFitWidth() => FitWidth();
    public void TriggerZoomToFit() => ZoomToFit();

    private void OnPaperSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PaperContainer == null) return;

        if (IsA5Selected)
        {
            PaperContainer.Width = A5Width;
            PaperContainer.Height = A5Height;
        }
        else
        {
            PaperContainer.Width = A4Width;
            PaperContainer.Height = A4Height;
        }

        FitWidth();
    }

    private void OnFitWidthClicked(object sender, RoutedEventArgs e) => FitWidth();
    private void OnZoomToFitClicked(object sender, RoutedEventArgs e) => ZoomToFit();
    private void OnZoomInClicked(object sender, RoutedEventArgs e) => SetZoom(_currentZoom + 0.15);
    private void OnZoomOutClicked(object sender, RoutedEventArgs e) => SetZoom(_currentZoom - 0.15);
    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    private void OnPrintClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDialog = new PrintDialog();
            
            // Configure explicit PrintTicket with Portrait and appropriate PageMediaSize
            var printTicket = printDialog.PrintTicket ?? new PrintTicket();
            printTicket.PageOrientation = PageOrientation.Portrait;
            printTicket.PageMediaSize = IsA5Selected 
                ? new PageMediaSize(PageMediaSizeName.ISOA5) 
                : new PageMediaSize(PageMediaSizeName.ISOA4);
            printDialog.PrintTicket = printTicket;

            if (printDialog.ShowDialog() == true)
            {
                // Print isolated PrescriptionPaperView to ensure zoom-independent output without container drop shadow
                var printablePage = new PrescriptionPaperView();
                if (_prescription != null)
                {
                    printablePage.Populate(_prescription);
                }

                double pageWidth = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : (IsA5Selected ? A5Width : A4Width);
                double pageHeight = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : (IsA5Selected ? A5Height : A4Height);

                printablePage.Width = pageWidth;
                printablePage.Height = pageHeight;
                printablePage.Measure(new Size(pageWidth, pageHeight));
                printablePage.Arrange(new Rect(0, 0, pageWidth, pageHeight));
                printablePage.UpdateLayout();

                printDialog.PrintVisual(printablePage, $"Prescription - {_prescription?.PrescriptionNumber ?? "Document"}");
            }
        }
        catch (Exception ex)
        {
            CustomDialogWindow.ShowError(this, $"Print failed: {ex.Message}", "Print Error");
        }
    }
}
