using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DoctorRx.Presentation.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string? _busyMessage;

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

    public virtual Task InitializeAsync(object? parameter = null)
    {
        return Task.CompletedTask;
    }
}
