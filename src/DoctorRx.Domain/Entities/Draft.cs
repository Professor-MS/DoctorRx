using System;
using DoctorRx.Domain.Common;

namespace DoctorRx.Domain.Entities;

/// <summary>
/// Represents an in-progress prescription draft. Kept completely separate from finalized Prescriptions.
/// </summary>
public class Draft : EntityBase
{
    public Guid DraftKey { get; set; } = Guid.NewGuid();
    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public int PayloadVersion { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string AppVersion { get; set; } = "1.0.0";
}
