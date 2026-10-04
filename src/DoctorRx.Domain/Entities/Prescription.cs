using System;
using System.Collections.Generic;
using System.Linq;
using DoctorRx.Domain.Common;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Domain.Entities;

/// <summary>
/// Represents a prescription issued by a doctor to a patient.
/// Note: DoctorRx does NOT diagnose or calculate dosages. It acts as an accurate, professional recording tool.
/// </summary>
public class Prescription : AuditableEntity
{
    public string PrescriptionNumber { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }

    public DateTime PrescriptionDate { get; set; } = DateTime.Today;

    // Clinical observations recorded directly by the physician (no automated diagnosis)
    public string? ChiefComplaints { get; set; }
    public string? BloodPressure { get; set; }
    public string? PulseRate { get; set; }
    public string? Temperature { get; set; }
    public string? WeightKg { get; set; }
    public string? ClinicalNotes { get; set; }

    // Prescription directives
    public string? GeneralAdvice { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Draft;

    // Prescribed items
    public ICollection<PrescriptionMedicine> Items { get; set; } = new List<PrescriptionMedicine>();

    /// <summary>
    /// Adds a prescribed medicine preserving snapshot details.
    /// </summary>
    public void AddMedicine(PrescriptionMedicine item)
    {
        item.SortOrder = Items.Count + 1;
        item.PrescriptionId = Id;
        item.Prescription = this;
        Items.Add(item);
    }

    /// <summary>
    /// Finalizes the prescription. Once finalized, it marks the medical document as issued.
    /// </summary>
    public void FinalizePrescription()
    {
        if (Status == PrescriptionStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot finalize a cancelled prescription.");
        }
        Status = PrescriptionStatus.Finalized;
    }
}
