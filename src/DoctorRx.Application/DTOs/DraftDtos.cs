using System;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Application.DTOs;

public record DraftSummaryDto(
    Guid DraftKey,
    int? PatientId,
    string? PatientName,
    string? PatientRecordNumber,
    string? PatientAgeText,
    Gender? PatientGender,
    int MedicineCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    bool IsOlderThan30Days,
    bool IsCorrupt,
    string? ErrorMessage
)
{
    public string DisplayPatientName => string.IsNullOrWhiteSpace(PatientName) ? "Unspecified Patient" : PatientName;
    public string RelativeTimeText => $"Updated {UpdatedAtUtc.ToLocalTime():g}";
}
