using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using DoctorRx.Application.Interfaces;

namespace DoctorRx.Presentation.Services;

/// <summary>
/// Serializable window geometry and state stored under %LOCALAPPDATA%\DoctorRx\window-placement.json.
/// </summary>
public class WindowPlacementSettings
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMaximized { get; set; }
    public string? MonitorDeviceName { get; set; }
    public bool? IsSidebarCollapsedOverride { get; set; }
}

public interface IWindowPlacementService
{
    WindowPlacementSettings? LoadPlacement();
    void SavePlacement(WindowPlacementSettings settings);
    void ApplyPlacement(Window window);
    void PersistPlacement(Window window, bool? isSidebarCollapsedOverride = null);
    bool IsOnScreen(double left, double top, double width, double height);
}

public class WindowPlacementService : IWindowPlacementService
{
    private readonly IAppPaths _appPaths;
    private readonly string _settingsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public WindowPlacementService(IAppPaths appPaths)
    {
        _appPaths = appPaths;
        _settingsFilePath = Path.Combine(_appPaths.BaseDirectory, "window-placement.json");
    }

    public WindowPlacementSettings? LoadPlacement()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<WindowPlacementSettings>(json);
        }
        catch
        {
            return null;
        }
    }

    public void SavePlacement(WindowPlacementSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Non-critical persistence failure should never crash the workstation
        }
    }

    public void ApplyPlacement(Window window)
    {
        var settings = LoadPlacement();

        if (settings != null && IsOnScreen(settings.Left, settings.Top, settings.Width, settings.Height))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = settings.Left;
            window.Top = settings.Top;
            window.Width = Math.Max(LayoutBreakpoints.ShellMinWidth, settings.Width);
            window.Height = Math.Max(LayoutBreakpoints.ShellMinHeight, settings.Height);

            if (settings.IsMaximized)
            {
                window.WindowState = WindowState.Maximized;
            }
            else
            {
                window.WindowState = WindowState.Normal;
            }
        }
        else
        {
            // First run or saved bounds off-screen: center at ~85% of primary work area clamped
            var workArea = SystemParameters.WorkArea;
            double targetWidth = Math.Clamp(workArea.Width * 0.85, LayoutBreakpoints.ShellMinWidth, workArea.Width);
            double targetHeight = Math.Clamp(workArea.Height * 0.85, LayoutBreakpoints.ShellMinHeight, workArea.Height);
            double left = workArea.Left + (workArea.Width - targetWidth) / 2.0;
            double top = workArea.Top + (workArea.Height - targetHeight) / 2.0;

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = left;
            window.Top = top;
            window.Width = targetWidth;
            window.Height = targetHeight;
            window.WindowState = WindowState.Normal;
        }
    }

    public void PersistPlacement(Window window, bool? isSidebarCollapsedOverride = null)
    {
        bool isMaximized = window.WindowState == WindowState.Maximized;
        Rect normalBounds;

        if (isMaximized || window.WindowState == WindowState.Minimized)
        {
            normalBounds = window.RestoreBounds;
        }
        else
        {
            double width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
            double height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
            normalBounds = new Rect(window.Left, window.Top, width, height);
        }

        // Preserve existing override if null
        var existing = LoadPlacement();
        var sidebarOverride = isSidebarCollapsedOverride ?? existing?.IsSidebarCollapsedOverride;

        var settings = new WindowPlacementSettings
        {
            Left = normalBounds.Left,
            Top = normalBounds.Top,
            Width = Math.Max(LayoutBreakpoints.ShellMinWidth, normalBounds.Width),
            Height = Math.Max(LayoutBreakpoints.ShellMinHeight, normalBounds.Height),
            IsMaximized = isMaximized,
            MonitorDeviceName = existing?.MonitorDeviceName,
            IsSidebarCollapsedOverride = sidebarOverride
        };

        SavePlacement(settings);
    }

    public bool IsOnScreen(double left, double top, double width, double height)
    {
        if (double.IsNaN(left) || double.IsNaN(top) || double.IsNaN(width) || double.IsNaN(height) ||
            width < 100 || height < 100)
        {
            return false;
        }

        try
        {
            var monitors = NativeMethods.GetAllMonitors();
            if (monitors.Count == 0)
            {
                // Fallback to WPF VirtualScreen bounds
                var vsLeft = SystemParameters.VirtualScreenLeft;
                var vsTop = SystemParameters.VirtualScreenTop;
                var vsWidth = SystemParameters.VirtualScreenWidth;
                var vsHeight = SystemParameters.VirtualScreenHeight;

                var windowRect = new Rect(left, top, width, height);
                var virtualScreenRect = new Rect(vsLeft, vsTop, vsWidth, vsHeight);
                windowRect.Intersect(virtualScreenRect);

                return windowRect.Width >= 150 && windowRect.Height >= 150;
            }

            var rect = new Rect(left, top, width, height);

            foreach (var m in monitors)
            {
                var workRect = new Rect(m.WorkArea.Left, m.WorkArea.Top, m.WorkArea.Width, m.WorkArea.Height);
                var intersection = Rect.Intersect(rect, workRect);

                if (intersection.Width >= 150 && intersection.Height >= 150)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return true;
        }
    }
}
