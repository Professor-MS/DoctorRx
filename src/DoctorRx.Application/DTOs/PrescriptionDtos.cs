using System;
using System.Collections.Generic;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.ValueObjects;

namespace DoctorRx.Application.DTOs;

public record PrescriptionMedicineDto(
    int Id,
    int? MedicineId,
    string MedicineName,
    string? GenericName,
    string Form,
    string Strength,
    string Dose,
    string Frequency,
    string? Timing,
    MealRelation MealRelation,
    string? CustomMealRelationText,
    string? WithWhat,
    string Route,
    string Duration,
    string? Instructions,
    int SortOrder
)
{
    public string FormattedDirections => $"{Form} {Strength} - {Dose} ({Frequency}) for {Duration}".Trim();
}

public record PrescriptionSummaryDto(
    int Id,
    string PrescriptionNumber,
    int PatientId,
    string PatientName,
    string PatientAge,
    Gender PatientGender,
    DateOnly PrescriptionDate,
    PrescriptionStatus Status,
    int MedicineCount,
    DateOnly? FollowUpDate,
    int AmendmentNumber
);

public record PrescriptionDetailDto(
    int Id,
    string PrescriptionNumber,
    int PatientId,
    PatientSnapshot PatientSnapshot,
    int DoctorId,
    DoctorSnapshot DoctorSnapshot,
    DateOnly PrescriptionDate,
    string? ChiefComplaints,
    string? BloodPressure,
    string? PulseRate,
    string? Temperature,
    string? WeightKg,
    string? ClinicalNotes,
    string? GeneralAdvice,
    DateOnly? FollowUpDate,
    PrescriptionStatus Status,
    DateTime FinalizedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    int? ParentPrescriptionId,
    int AmendmentNumber,
    int Version,
    IReadOnlyList<PrescriptionMedicineDto> Items
);

public class CreatePrescriptionDto
{
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateOnly PrescriptionDate { get; set; }
    public string? ChiefComplaints { get; set; }
    public string? BloodPressure { get; set; }
    public string? PulseRate { get; set; }
    public string? Temperature { get; set; }
    public string? WeightKg { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? GeneralAdvice { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public List<CreatePrescriptionMedicineDto> Items { get; set; } = new();
}

public class CreatePrescriptionMedicineDto
{
    public int? MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string Form { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string Dose { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string? Timing { get; set; }
    public MealRelation MealRelation { get; set; } = MealRelation.AsDirected;
    public string? CustomMealRelationText { get; set; }
    public string? WithWhat { get; set; }
    public string Route { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public int SortOrder { get; set; }
}
