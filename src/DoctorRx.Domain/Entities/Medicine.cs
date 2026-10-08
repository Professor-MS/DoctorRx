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
    public int UsageCount { get; set; } = 0;
    public System.DateTime? LastUsedAtUtc { get; set; }

    // Navigation properties
    public System.Collections.Generic.ICollection<MedicineSearchToken> SearchTokens { get; set; } = new System.Collections.Generic.List<MedicineSearchToken>();

    /// <summary>
    /// Gets formatted display text for auto-complete and dropdowns.
    /// </summary>
    public string DisplayTitle => string.IsNullOrWhiteSpace(Strength)
        ? $"{Name} ({Form})"
        : $"{Name} {Strength} ({Form})";
}
