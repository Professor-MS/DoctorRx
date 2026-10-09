using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DoctorRx.Presentation.Services;

namespace DoctorRx.Presentation.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string? _busyMessage;
    private LayoutMode _layoutMode = LayoutMode.Normal;

    public virtual NavigationSection NavigationSection => NavigationSection.Dashboard;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string? BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    public LayoutMode LayoutMode
    {
        get => _layoutMode;
        set
        {
            if (SetProperty(ref _layoutMode, value))
            {
                OnPropertyChanged(nameof(IsCompact));
                OnPropertyChanged(nameof(IsNormal));
                OnPropertyChanged(nameof(IsWide));
                OnLayoutModeChanged(value);
            }
        }
    }

    public bool IsCompact => LayoutMode == LayoutMode.Compact;
    public bool IsNormal => LayoutMode == LayoutMode.Normal;
    public bool IsWide => LayoutMode == LayoutMode.Wide;

    public virtual void UpdateLayoutMode(LayoutMode mode)
    {
        LayoutMode = mode;
    }

    protected virtual void OnLayoutModeChanged(LayoutMode newMode) { }

    public virtual Task InitializeAsync(object? parameter = null)
    {
        return Task.CompletedTask;
    }
}
