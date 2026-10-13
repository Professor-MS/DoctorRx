using System;
using System.Windows;
using System.Windows.Controls;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Presentation.Views;

public partial class InvestigationPaperView : UserControl
{
    public TextBlock RequestNumberTextElement => RequestNumberText;
    public TextBlock PatientNameTextElement => PatientNameText;
    public TextBlock DoctorNameTextElement => DoctorNameText;
    public ItemsControl InvestigationsItemsControlElement => InvestigationsItemsControl;
    public Border ClinicalNotesBorderElement => ClinicalNotesBorder;
    public Border InstructionsBorderElement => InstructionsBorder;

    public InvestigationPaperView()
    {
        InitializeComponent();
    }

    public void Populate(InvestigationRequestDetailDto req)
    {
        // Clinic & Doctor Header
        ClinicNameText.Text = !string.IsNullOrWhiteSpace(req.ClinicName) 
            ? req.ClinicName 
            : "Medical Diagnostic Center";

        DoctorNameText.Text = !string.IsNullOrWhiteSpace(req.DoctorName) 
            ? req.DoctorName 
            : "Consultant Physician";

        DoctorQualificationText.Text = req.DoctorQualification ?? string.Empty;
        DoctorQualificationText.Visibility = string.IsNullOrWhiteSpace(req.DoctorQualification) ? Visibility.Collapsed : Visibility.Visible;

        DoctorSpecializationText.Text = req.DoctorSpecialization ?? string.Empty;
        DoctorSpecializationText.Visibility = string.IsNullOrWhiteSpace(req.DoctorSpecialization) ? Visibility.Collapsed : Visibility.Visible;

        DoctorRegText.Text = !string.IsNullOrWhiteSpace(req.DoctorRegistration) 
            ? $"Reg. No.: {req.DoctorRegistration}" 
            : string.Empty;
        DoctorRegText.Visibility = string.IsNullOrWhiteSpace(req.DoctorRegistration) ? Visibility.Collapsed : Visibility.Visible;

        var contactParts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(req.ClinicPhone)) contactParts.Add($"Tel: {req.ClinicPhone}");
        if (!string.IsNullOrWhiteSpace(req.ClinicAddress)) contactParts.Add(req.ClinicAddress);
        ClinicContactText.Text = string.Join(" | ", contactParts);
        ClinicContactText.Visibility = contactParts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Order Metadata
        RequestDateText.Text = $"Date: {req.RequestDate:dd MMM yyyy}";
        RequestNumberText.Text = $"Order # {req.RequestNumber}";

        // Patient Details
        PatientNameText.Text = req.PatientName;
        PatientAgeText.Text = string.IsNullOrWhiteSpace(req.PatientAge) ? "Not recorded" : req.PatientAge;
        PatientGenderText.Text = req.PatientGender.ToString();
        PatientRecordText.Text = req.PatientRecordNumber;

        // Clinical Notes / Indication
        if (!string.IsNullOrWhiteSpace(req.ClinicalNotes))
        {
            ClinicalNotesText.Text = req.ClinicalNotes;
            ClinicalNotesBorder.Visibility = Visibility.Visible;
        }
        else
        {
            ClinicalNotesBorder.Visibility = Visibility.Collapsed;
        }

        // Investigations Items
        InvestigationsItemsControl.ItemsSource = req.Items;

        // General Patient Instructions
        if (!string.IsNullOrWhiteSpace(req.Instructions))
        {
            InstructionsText.Text = req.Instructions;
            InstructionsBorder.Visibility = Visibility.Visible;
        }
        else
        {
            InstructionsBorder.Visibility = Visibility.Collapsed;
        }

        // Footer Timestamp
        var localTime = req.CreatedAtUtc.ToLocalTime();
        FooterMetadataText.Text = $"Generated: {localTime:dd MMM yyyy hh:mm tt} | DoctorRx Clinical System";
    }
}
