using DoctorRx.Domain.Common;

namespace DoctorRx.Domain.Entities;

/// <summary>
/// Master repository record for medicines and formulations.
/// Adheres strictly to the safety principle: stores only formulation identifiers.
/// No clinical defaults or dosage advice are stored in the catalog.
/// </summary>
public class Medicine : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string Form { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets formatted display text for auto-complete and dropdowns.
    /// </summary>
    public string DisplayTitle => string.IsNullOrWhiteSpace(Strength)
        ? $"{Name} ({Form})"
        : $"{Name} {Strength} ({Form})";
}
