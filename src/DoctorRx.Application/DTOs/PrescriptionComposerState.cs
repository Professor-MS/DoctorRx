using System;
using System.Collections.Generic;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Application.DTOs;

public class PrescriptionComposerState
{
    public Guid DraftKey { get; set; } = Guid.NewGuid();
    public int SchemaVersion { get; set; } = 1;

    // Patient reference & cached display fallback
    public int? PatientId { get; set; }
    public string? PatientName { get; set; }
    public string? PatientRecordNumber { get; set; }
    public string? PatientAgeText { get; set; }
    public Gender? PatientGender { get; set; }
    public string? PatientPhone { get; set; }
    public string? PatientKnownAllergies { get; set; }
    public DateOnly? PatientLastVisitDate { get; set; }

    public DateOnly VisitDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    // Optional domain observations
    public string? ChiefComplaints { get; set; }
    public string? BloodPressure { get; set; }
    public string? PulseRate { get; set; }
    public string? Temperature { get; set; }
    public string? WeightKg { get; set; }
    public string? ClinicalNotes { get; set; }

    // Prescribed medicines
    public List<PrescriptionMedicineRowState> Items { get; set; } = new();

    // Directives
    public string? GeneralAdvice { get; set; }
    public FollowUpSettingState FollowUp { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsFinalized { get; set; }
}
