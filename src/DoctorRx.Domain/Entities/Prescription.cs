using System;
using System.Collections.Generic;
using System.Linq;
using DoctorRx.Domain.Common;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Exceptions;
using DoctorRx.Domain.ValueObjects;

namespace DoctorRx.Domain.Entities;

/// <summary>
/// Represents an immutable clinical prescription issued by a physician.
/// Enforces domain lifecycle: Finalized -> Cancelled or Superseded.
/// </summary>
public class Prescription : AuditableEntity
{
    public string PrescriptionNumber { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }

    public DateOnly PrescriptionDate { get; set; }

    // Snapshots: frozen at finalization
    public DoctorSnapshot DoctorSnapshot { get; set; } = new();
    public PatientSnapshot PatientSnapshot { get; set; } = new();

    // Clinical observations recorded directly by the physician
    public string? ChiefComplaints { get; set; }
    public string? BloodPressure { get; set; }
    public string? PulseRate { get; set; }
    public string? Temperature { get; set; }
    public string? WeightKg { get; set; }
    public string? ClinicalNotes { get; set; }

    // Prescription directives
    public string? GeneralAdvice { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public string? FollowUpText { get; set; }

    // Lifecycle state
    public PrescriptionStatus Status { get; private set; } = PrescriptionStatus.Finalized;
    public DateTime FinalizedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public int? ParentPrescriptionId { get; private set; }
    public Prescription? ParentPrescription { get; private set; }
    public int AmendmentNumber { get; private set; }
    public int Version { get; set; } = 1;

    // Prescribed items
    private readonly List<PrescriptionMedicine> _items = new();
    public ICollection<PrescriptionMedicine> Items => _items;

    // EF Core constructor
    public Prescription()
    {
    }

    /// <summary>
    /// Creates a finalized prescription with immutable snapshots and validated medicine items.
    /// </summary>
    public static Prescription CreateFinalized(
        string prescriptionNumber,
        int patientId,
        int doctorId,
        DateOnly prescriptionDate,
        DoctorSnapshot doctorSnapshot,
        PatientSnapshot patientSnapshot,
        DateTime finalizedAtUtc,
        int? parentPrescriptionId = null,
        int amendmentNumber = 0,
        string? chiefComplaints = null,
        string? bloodPressure = null,
        string? pulseRate = null,
        string? temperature = null,
        string? weightKg = null,
        string? clinicalNotes = null,
        string? generalAdvice = null,
        DateOnly? followUpDate = null,
        string? followUpText = null)
    {
        if (string.IsNullOrWhiteSpace(prescriptionNumber))
        {
            throw new DomainRuleException("Prescription number is required.");
        }
        if (patientId <= 0)
        {
            throw new DomainRuleException("A valid patient is required.");
        }
        if (doctorId <= 0)
        {
            throw new DomainRuleException("A valid doctor is required.");
        }

        var rx = new Prescription
        {
            PrescriptionNumber = prescriptionNumber.Trim(),
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = prescriptionDate,
            DoctorSnapshot = doctorSnapshot ?? throw new DomainRuleException("Doctor snapshot is required."),
            PatientSnapshot = patientSnapshot ?? throw new DomainRuleException("Patient snapshot is required."),
            Status = PrescriptionStatus.Finalized,
            FinalizedAtUtc = finalizedAtUtc,
            ParentPrescriptionId = parentPrescriptionId,
            AmendmentNumber = amendmentNumber,
            Version = 1,
            ChiefComplaints = chiefComplaints?.Trim(),
            BloodPressure = bloodPressure?.Trim(),
            PulseRate = pulseRate?.Trim(),
            Temperature = temperature?.Trim(),
            WeightKg = weightKg?.Trim(),
            ClinicalNotes = clinicalNotes?.Trim(),
            GeneralAdvice = generalAdvice?.Trim(),
            FollowUpDate = followUpDate,
            FollowUpText = followUpText?.Trim(),
            CreatedAtUtc = finalizedAtUtc
        };

        return rx;
    }

    /// <summary>
    /// Adds a prescribed medicine line to the prescription.
    /// </summary>
    public void AddMedicine(PrescriptionMedicine item)
    {
        if (string.IsNullOrWhiteSpace(item.MedicineName))
        {
            throw new DomainRuleException($"Medicine name is required for line {_items.Count + 1}.");
        }
        if (string.IsNullOrWhiteSpace(item.Dose))
        {
            throw new DomainRuleException($"Dose is required for medicine '{item.MedicineName}'.");
        }
        if (string.IsNullOrWhiteSpace(item.Frequency))
        {
            throw new DomainRuleException($"Frequency is required for medicine '{item.MedicineName}'.");
        }
        item.Form ??= string.Empty;

        item.SortOrder = _items.Count + 1;
        item.PrescriptionId = Id;
        item.Prescription = this;
        _items.Add(item);
    }

    /// <summary>
    /// Cancels the prescription. Requires a non-empty cancellation reason.
    /// </summary>
    public void Cancel(string reason, DateTime cancelledAtUtc)
    {
        if (Status == PrescriptionStatus.Cancelled)
        {
            throw new DomainRuleException("Prescription is already cancelled.");
        }
        if (Status == PrescriptionStatus.Superseded)
        {
            throw new DomainRuleException("Cannot cancel a superseded prescription.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("A reason is required to cancel a prescription.");
        }

        Status = PrescriptionStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        CancellationReason = reason.Trim();
        UpdatedAtUtc = cancelledAtUtc;
        Version++;
    }

    /// <summary>
    /// Marks the prescription as superseded by a newer amended prescription.
    /// </summary>
    public void MarkSuperseded(DateTime supersededAtUtc)
    {
        if (Status == PrescriptionStatus.Cancelled)
        {
            throw new DomainRuleException("Cannot supersede a cancelled prescription.");
        }
        if (Status == PrescriptionStatus.Superseded)
        {
            throw new DomainRuleException("Prescription is already superseded.");
        }

        Status = PrescriptionStatus.Superseded;
        UpdatedAtUtc = supersededAtUtc;
        Version++;
    }
}
