namespace DoctorRx.Application.Interfaces;

public interface IAppPaths
{
    string BaseDirectory { get; }
    string DataDirectory { get; }
    string DatabasePath { get; }
    string BackupsDirectory { get; }
    string DraftsDirectory { get; }
    string LogsDirectory { get; }
    string AssetsDirectory { get; }
}
