using System;
using System.Collections.Generic;
using DoctorRx.Domain.Common;

namespace DoctorRx.Domain.Entities;

/// <summary>
/// Diagnostic investigation order issued by a physician for a patient.
/// Can contain multiple imaging, laboratory, and specialized diagnostic tests.
/// </summary>
public class InvestigationRequest : AuditableEntity
{
    /// <summary>
    /// Sequential unique request identifier, e.g. "INV-20261013-0001".
    /// </summary>
    public string RequestNumber { get; set; } = string.Empty;

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public DateOnly RequestDate { get; set; }

    /// <summary>
    /// Clinical notes, provisional diagnosis, or reason for requesting investigations.
    /// </summary>
    public string? ClinicalNotes { get; set; }

    /// <summary>
    /// General instructions or special patient directives (e.g. "Fasting required", "Bring previous imaging reports").
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Immutable serialized snapshot of doctor credentials at the time the request was generated.
    /// </summary>
    public string? DoctorSnapshotJson { get; set; }

    /// <summary>
    /// Immutable serialized snapshot of patient demographic details at the time the request was generated.
    /// </summary>
    public string? PatientSnapshotJson { get; set; }

    /// <summary>
    /// Status of the request: "Active", "Cancelled".
    /// </summary>
    public string Status { get; set; } = "Active";

    public bool IsCancelled => string.Equals(Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Collection of individual investigation items ordered in this request.
    /// </summary>
    public ICollection<InvestigationRequestItem> Items { get; set; } = new List<InvestigationRequestItem>();
}
