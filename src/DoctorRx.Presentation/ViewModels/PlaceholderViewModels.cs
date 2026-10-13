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
