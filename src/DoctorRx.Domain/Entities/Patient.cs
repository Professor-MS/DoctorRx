using System;
using System.Collections.Generic;
using DoctorRx.Domain.Common;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Domain.Entities;

/// <summary>
/// Represents a patient registered in the clinic.
/// </summary>
public class Patient : AuditableEntity
{
    public string RecordNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public DateOnly? AgeRecordedDate { get; set; }
    public Gender Gender { get; set; } = Gender.NotSpecified;
    public string? Phone { get; set; }
    public string? PhoneDigits { get; set; }
    public string? Address { get; set; }
    public string? MedicalHistoryNotes { get; set; }
    public string? KnownAllergies { get; set; }

    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateOnly? LastVisitDate { get; set; }

    // Navigation properties
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    public ICollection<PatientSearchToken> SearchTokens { get; set; } = new List<PatientSearchToken>();

    /// <summary>
    /// Calculates the patient's age as of a specific date (e.g. visit date or today).
    /// </summary>
    public int CalculateAge(DateOnly asOfDate)
    {
        if (DateOfBirth.HasValue)
        {
            var dob = DateOfBirth.Value;
            var age = asOfDate.Year - dob.Year;
            if (dob > asOfDate.AddYears(-age)) age--;
            return Math.Max(0, age);
        }

        if (Age.HasValue && AgeRecordedDate.HasValue)
        {
            var yearsPassed = asOfDate.Year - AgeRecordedDate.Value.Year;
            if (AgeRecordedDate.Value > asOfDate.AddYears(-yearsPassed)) yearsPassed--;
            return Math.Max(0, Age.Value + Math.Max(0, yearsPassed));
        }

        return Age ?? 0;
    }

    /// <summary>
    /// Formatted age string as of a specific date (e.g. "35 yrs" or "6 mos").
    /// </summary>
    public string FormatAgeAsOf(DateOnly asOfDate)
    {
        if (DateOfBirth.HasValue)
        {
            var dob = DateOfBirth.Value;
            var years = asOfDate.Year - dob.Year;
            if (dob > asOfDate.AddYears(-years)) years--;
            
            if (years >= 1) return $"{years} yrs";

            // Under 1 year old: show months
            var months = (asOfDate.Year - dob.Year) * 12 + asOfDate.Month - dob.Month;
            if (asOfDate.Day < dob.Day) months--;
            return months <= 1 ? "1 mo" : $"{Math.Max(0, months)} mos";
        }

        var age = CalculateAge(asOfDate);
        return $"{age} yrs";
    }

    public void Archive(DateTime utcNow)
    {
        IsArchived = true;
        ArchivedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void Restore(DateTime utcNow)
    {
        IsArchived = false;
        ArchivedAtUtc = null;
        UpdatedAtUtc = utcNow;
    }

    public DoctorRx.Domain.ValueObjects.PatientSnapshot ToSnapshot(DateOnly visitDate)
    {
        return new DoctorRx.Domain.ValueObjects.PatientSnapshot(
            Name,
            Gender,
            FormatAgeAsOf(visitDate),
            Phone,
            KnownAllergies
        );
    }
}
