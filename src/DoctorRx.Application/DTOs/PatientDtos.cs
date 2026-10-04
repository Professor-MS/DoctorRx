using System;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Application.DTOs;

public record PatientDto(
    int Id,
    string Name,
    DateTime? DateOfBirth,
    int Age,
    Gender Gender,
    string? Phone,
    string? Address,
    string? MedicalHistoryNotes,
    string? KnownAllergies,
    DateTime CreatedAtUtc,
    DateTime? LastVisitDateUtc
)
{
    public string DisplayAgeGender => $"{Age} yrs / {Gender}";
    public string FormattedPhone => string.IsNullOrWhiteSpace(Phone) ? "No contact" : Phone;
}

public class CreatePatientDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public Gender Gender { get; set; } = Gender.NotSpecified;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? MedicalHistoryNotes { get; set; }
    public string? KnownAllergies { get; set; }
}

public class UpdatePatientDto : CreatePatientDto
{
    public int Id { get; set; }
}
