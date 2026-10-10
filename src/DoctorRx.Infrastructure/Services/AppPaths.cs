using System;
using System.IO;
using DoctorRx.Application.Interfaces;

namespace DoctorRx.Infrastructure.Services;

public class AppPaths : IAppPaths
{
    public const string EnvironmentVariableOverride = "DOCTORRX_DATA_DIR";

    public string BaseDirectory { get; }
    public string DataDirectory { get; }
    public string DatabasePath { get; }
    public string BackupsDirectory { get; }
    public string AutoBackupsDirectory { get; }
    public string SafetyBackupsDirectory { get; }
    public string LogsDirectory { get; }
    public string AssetsDirectory { get; }

    public AppPaths(string? basePathOverride = null)
    {
        var envOverride = Environment.GetEnvironmentVariable(EnvironmentVariableOverride);
        var baseDir = basePathOverride
            ?? (string.IsNullOrWhiteSpace(envOverride) ? null : envOverride)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DoctorRx");

        BaseDirectory = Path.GetFullPath(baseDir);
        DataDirectory = Path.Combine(BaseDirectory, "Data");
        DatabasePath = Path.Combine(DataDirectory, "doctorrx.db");
        BackupsDirectory = Path.Combine(BaseDirectory, "Backups");
        AutoBackupsDirectory = Path.Combine(BackupsDirectory, "Auto");
        SafetyBackupsDirectory = Path.Combine(BackupsDirectory, "Safety");
        LogsDirectory = Path.Combine(BaseDirectory, "Logs");
        AssetsDirectory = Path.Combine(BaseDirectory, "Assets");

        EnsureDirectoriesExist();
    }

    private void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(AutoBackupsDirectory);
        Directory.CreateDirectory(SafetyBackupsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(AssetsDirectory);
    }
}
