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
    public override NavigationSection NavigationSection => NavigationSection.PrescriptionHistory;

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
