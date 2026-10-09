using System;
using System.Windows;
using System.Windows.Controls;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Presentation.Views;

public partial class PrescriptionPaperView : UserControl
{
    public Border ObservationsBorderElement => ObservationsBorder;
    public Border AdviceBorderElement => AdviceBorder;
    public TextBlock FooterMetadataTextElement => FooterMetadataText;

    public PrescriptionPaperView()
    {
        InitializeComponent();
    }

    public void Populate(PrescriptionDetailDto p)
    {
        if (p.DoctorSnapshot != null)
        {
            ClinicNameText.Text = !string.IsNullOrWhiteSpace(p.DoctorSnapshot.ClinicName) 
                ? p.DoctorSnapshot.ClinicName 
                : "DoctorRx Medical Clinic";

            DoctorNameText.Text = !string.IsNullOrWhiteSpace(p.DoctorSnapshot.DisplayName) 
                ? p.DoctorSnapshot.DisplayName 
                : "Attending Physician";

            DoctorQualificationText.Text = p.DoctorSnapshot.Qualification;
            DoctorSpecializationText.Text = p.DoctorSnapshot.Specialization;

            var regLabel = p.DoctorSnapshot.SafeRegistrationLabel;
            DoctorRegText.Text = !string.IsNullOrWhiteSpace(p.DoctorSnapshot.RegistrationNumber) 
                ? $"{regLabel}: {p.DoctorSnapshot.RegistrationNumber}" 
                : string.Empty;
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

        // Omit empty observations / vitals
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

        // Omit empty advice & follow-up sections
        bool hasAdvice = !string.IsNullOrWhiteSpace(p.GeneralAdvice);
        bool hasFollowUp = !string.IsNullOrWhiteSpace(p.FollowUpText);

        if (hasAdvice || hasFollowUp)
        {
            AdviceBorder.Visibility = Visibility.Visible;
            if (hasAdvice)
            {
                AdviceText.Text = p.GeneralAdvice;
                AdviceSection.Visibility = Visibility.Visible;
            }
            else
            {
                AdviceSection.Visibility = Visibility.Collapsed;
            }

            if (hasFollowUp)
            {
                FollowUpText.Text = p.FollowUpText;
                FollowUpSection.Visibility = Visibility.Visible;
            }
            else
            {
                FollowUpSection.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            AdviceBorder.Visibility = Visibility.Collapsed;
        }

        // Show issue time in local time, omit "Document Validated"
        var localTime = p.FinalizedAtUtc.ToLocalTime();
        FooterMetadataText.Text = $"Issued: {localTime:dd MMM yyyy hh:mm tt}";
    }
}
