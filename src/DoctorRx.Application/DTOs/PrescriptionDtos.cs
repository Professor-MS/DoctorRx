using System;
using System.Collections.Generic;
using DoctorRx.Domain.Enums;

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
    int PatientAge,
    Gender PatientGender,
    DateTime PrescriptionDate,
    PrescriptionStatus Status,
    int MedicineCount,
    DateTime? FollowUpDate
);

public record PrescriptionDetailDto(
    int Id,
    string PrescriptionNumber,
    int PatientId,
    PatientDto Patient,
    int DoctorId,
    DoctorDto Doctor,
    DateTime PrescriptionDate,
    string? ChiefComplaints,
    string? BloodPressure,
    string? PulseRate,
    string? Temperature,
    string? WeightKg,
    string? ClinicalNotes,
    string? GeneralAdvice,
    DateTime? FollowUpDate,
    PrescriptionStatus Status,
    IReadOnlyList<PrescriptionMedicineDto> Items,
    DateTime CreatedAtUtc
);

public class CreatePrescriptionDto
{
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime PrescriptionDate { get; set; } = DateTime.Today;
    public string? ChiefComplaints { get; set; }
    public string? BloodPressure { get; set; }
    public string? PulseRate { get; set; }
    public string? Temperature { get; set; }
    public string? WeightKg { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? GeneralAdvice { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public List<CreatePrescriptionMedicineDto> Items { get; set; } = new();
}

public class CreatePrescriptionMedicineDto
{
    public int? MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string Form { get; set; } = "Tablet";
    public string Strength { get; set; } = string.Empty;
    public string Dose { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string? Timing { get; set; }
    public MealRelation MealRelation { get; set; } = MealRelation.AsDirected;
    public string? CustomMealRelationText { get; set; }
    public string Route { get; set; } = "Oral";
    public string Duration { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public int SortOrder { get; set; }
}
