using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Presentation.Services;

namespace DoctorRx.Presentation.ViewModels;

public abstract class ScaffoldedFeatureViewModel : ViewModelBase
{
    public string FeatureName { get; }
    public string FeatureDescription { get; }
    public string PhaseMilestone { get; }
    public ObservableCollection<string> PlannedCapabilities { get; } = new();

    public ScaffoldedFeatureViewModel(string name, string description, string phase, string[] capabilities)
    {
        FeatureName = name;
        FeatureDescription = description;
        PhaseMilestone = phase;
        foreach (var c in capabilities)
        {
            PlannedCapabilities.Add(c);
        }
    }
}


public class PrescriptionHistoryViewModel : ScaffoldedFeatureViewModel
{
    public PrescriptionHistoryViewModel() : base(
        "Prescription Archive",
        "Search, filter, inspect, and reprint past prescriptions with immutable historical medicine records.",
        "Phase 2 Prescription Management",
        new[]
        {
            "Date range filtering & patient MRN search",
            "View historical prescriptions with exact medicine snapshots",
            "Re-issue or duplicate previous prescription as a new draft",
            "Export past prescription records to PDF",
            "Audit trail tracking changes and issuance dates"
        })
    {
    }
}

public class MedicinesViewModel : ScaffoldedFeatureViewModel
{
    public MedicinesViewModel() : base(
        "Medicine Catalog Management",
        "Manage the clinic's local formulary, brands, generic equivalents, standard strengths, and default dosing instructions.",
        "Phase 2 Formulary Management",
        new[]
        {
            "Browse and search local medicine catalog",
            "Add new brands and generic formulations",
            "Set default forms, strengths, frequencies, and meal relations",
            "Import and export medicine catalog to CSV/JSON",
            "Disable discontinued formulations without affecting historical records"
        })
    {
    }
}

public class SettingsViewModel : ScaffoldedFeatureViewModel
{
    public SettingsViewModel() : base(
        "Clinic & Application Settings",
        "Configure doctor credentials, clinic branding, prescription pad layout, printer presets, and database backup preferences.",
        "Phase 2 Configuration",
        new[]
        {
            "Doctor profile (name, qualifications, PMDC/license number, specialization)",
            "Clinic details (header text, address, contact numbers, footer disclaimer)",
            "Logo upload & digital signature integration",
            "Paper size configuration (A4, A5, custom dimensions, margins)",
            "Printer preferences & Microsoft Print to PDF defaults",
            "Automated SQLite backup schedule and restore utility",
            "Application security PIN and auto-lock"
        })
    {
    }
}
