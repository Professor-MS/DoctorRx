using System;
using System.Collections.Generic;
using DoctorRx.Domain.Common;
using DoctorRx.Domain.Exceptions;

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
    /// Indicates whether the investigation request is finalized and tamper-sealed.
    /// </summary>
    public bool IsSealed { get; set; }

    public void Seal()
    {
        IsSealed = true;
    }

    /// <summary>
    /// Reason provided by clinician when request is cancelled.
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    /// UTC timestamp when request was marked cancelled.
    /// </summary>
    public DateTime? CancelledAtUtc { get; set; }

    /// <summary>
    /// Cancels the investigation request. Requires a non-empty cancellation reason.
    /// </summary>
    public void Cancel(string reason, DateTime cancelledAtUtc)
    {
        if (string.Equals(Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleException("Investigation request is already cancelled.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("A reason is required to cancel an investigation request.");
        }

        Status = "Cancelled";
        CancellationReason = reason.Trim();
        CancelledAtUtc = cancelledAtUtc;
        UpdatedAtUtc = cancelledAtUtc;
    }

    /// <summary>
    /// Collection of individual investigation items ordered in this request.
    /// </summary>
    public ICollection<InvestigationRequestItem> Items { get; set; } = new List<InvestigationRequestItem>();
}
