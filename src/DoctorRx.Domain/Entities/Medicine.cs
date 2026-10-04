using DoctorRx.Domain.Common;

namespace DoctorRx.Domain.Entities;

/// <summary>
/// Master repository record for medicines and formulations.
/// </summary>
public class Medicine : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string Form { get; set; } = "Tablet";
    public string Strength { get; set; } = string.Empty;
    public string? DefaultDose { get; set; }
    public string? DefaultFrequency { get; set; }
    public string? DefaultRoute { get; set; } = "Oral";
    public string? DefaultInstructions { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets formatted display text for auto-complete and dropdowns.
    /// </summary>
    public string DisplayTitle => string.IsNullOrWhiteSpace(Strength)
        ? $"{Name} ({Form})"
        : $"{Name} {Strength} ({Form})";
}
