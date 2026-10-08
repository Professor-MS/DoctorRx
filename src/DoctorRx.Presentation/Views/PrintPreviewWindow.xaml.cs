using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Presentation.Views;

public partial class PrintPreviewWindow : Window
{
    private double _currentZoom = 1.0;

    public PrintPreviewWindow(PrescriptionDetailDto? prescription, Window? owner = null)
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
            PopulatePrescription(prescription);
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

    private void PopulatePrescription(PrescriptionDetailDto p)
    {
        if (p.DoctorSnapshot != null)
        {
            ClinicNameText.Text = !string.IsNullOrWhiteSpace(p.DoctorSnapshot.ClinicName) ? p.DoctorSnapshot.ClinicName : "DoctorRx Medical Clinic";
            DoctorNameText.Text = !string.IsNullOrWhiteSpace(p.DoctorSnapshot.Name) ? $"Dr. {p.DoctorSnapshot.Name}" : "Attending Physician";
            DoctorQualificationText.Text = p.DoctorSnapshot.Qualification;
            DoctorSpecializationText.Text = p.DoctorSnapshot.Specialization;
            DoctorRegText.Text = !string.IsNullOrWhiteSpace(p.DoctorSnapshot.RegistrationNumber) ? $"PMC/PMDC: {p.DoctorSnapshot.RegistrationNumber}" : string.Empty;
        }

        PrescriptionDateText.Text = $"Date: {p.PrescriptionDate:dd MMM yyyy}";
        PrescriptionNumberText.Text = $"Rx # {p.PrescriptionNumber}";

        if (p.PatientSnapshot != null)
        {
            PatientNameText.Text = p.PatientSnapshot.Name;
            PatientAgeText.Text = p.PatientSnapshot.AgeText;
            PatientGenderText.Text = p.PatientSnapshot.Gender.ToString();
            PatientRecordText.Text = $"P-{p.PatientId:D4}";
        }

        if (!string.IsNullOrWhiteSpace(p.ChiefComplaints))
        {
            ChiefComplaintsText.Text = p.ChiefComplaints;
            ObservationsBorder.Visibility = Visibility.Visible;
        }
        else
        {
            ObservationsBorder.Visibility = Visibility.Collapsed;
        }

        MedicinesItemsControl.ItemsSource = p.Items;

        if (!string.IsNullOrWhiteSpace(p.GeneralAdvice) || !string.IsNullOrWhiteSpace(p.FollowUpText))
        {
            AdviceText.Text = p.GeneralAdvice;
            FollowUpText.Text = p.FollowUpText;
            AdviceBorder.Visibility = Visibility.Visible;
        }
        else
        {
            AdviceBorder.Visibility = Visibility.Collapsed;
        }

        FooterMetadataText.Text = $"Issued: {p.FinalizedAtUtc:dd MMM yyyy hh:mm tt UTC} • Document Validated";
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
        if (DocumentScrollViewer.ActualWidth > 80 && DocumentScrollViewer.ActualHeight > 80)
        {
            var scaleX = (DocumentScrollViewer.ActualWidth - 64) / PaperContainer.Width;
            var scaleY = (DocumentScrollViewer.ActualHeight - 64) / 900.0;
            SetZoom(Math.Min(scaleX, scaleY));
        }
    }

    public double CurrentZoom => _currentZoom;
    public void TriggerFitWidth() => FitWidth();
    public void TriggerZoomToFit() => ZoomToFit();

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
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintVisual(PaperContainer, "Prescription Document");
            }
        }
        catch (Exception ex)
        {
            CustomDialogWindow.ShowError(this, $"Print failed: {ex.Message}", "Print Error");
        }
    }
}
