namespace DoctorRx.Presentation.Services;

/// <summary>
/// Defines the responsive layout modes for DoctorRx desktop shell and clinical screens.
/// </summary>
public enum LayoutMode
{
    /// <summary>
    /// Window width &lt; 1024 DIPs. Sidebar collapses to 64px icon rail; forms collapse to single-column; secondary badges hidden.
    /// </summary>
    Compact,

    /// <summary>
    /// Window width 1024 to 1439 DIPs. Standard desktop layout with 240px sidebar.
    /// </summary>
    Normal,

    /// <summary>
    /// Window width &gt;= 1440 DIPs. Wide display layout with 240px sidebar and two-column clinical forms.
    /// </summary>
    Wide
}

/// <summary>
/// Breakpoints and shell layout constants shared across DoctorRx views and tests.
/// </summary>
public static class LayoutBreakpoints
{
    /// <summary>Width boundary below which shell enters Compact mode (1024 DIPs).</summary>
    public const double CompactMaxWidth = 1024.0;

    /// <summary>Width boundary above which shell enters Wide mode (1440 DIPs).</summary>
    public const double NormalMaxWidth = 1440.0;

    /// <summary>Absolute minimum supported application size (960 x 520 DIPs).</summary>
    public const double MinimumSupportedWidth = 960.0;
    public const double MinimumSupportedHeight = 520.0;

    /// <summary>Shell window clamping minimum dimensions (900 x 520 DIPs).</summary>
    public const double ShellMinWidth = 900.0;
    public const double ShellMinHeight = 520.0;

    /// <summary>Expanded sidebar width (Wide and Normal modes).</summary>
    public const double SidebarExpandedWidth = 240.0;

    /// <summary>Collapsed sidebar icon rail width (Compact mode).</summary>
    public const double SidebarCollapsedWidth = 64.0;

    /// <summary>
    /// Determines the active layout mode based on DIP width.
    /// </summary>
    public static LayoutMode DetermineMode(double windowWidth)
    {
        if (windowWidth < CompactMaxWidth)
        {
            return LayoutMode.Compact;
        }

        if (windowWidth < NormalMaxWidth)
        {
            return LayoutMode.Normal;
        }

        return LayoutMode.Wide;
    }
}
