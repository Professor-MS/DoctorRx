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
    public string Name { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public Gender Gender { get; set; } = Gender.NotSpecified;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? MedicalHistoryNotes { get; set; }
    public string? KnownAllergies { get; set; }

    // Navigation properties
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

    /// <summary>
    /// Gets calculated age if DateOfBirth is available, otherwise returns the recorded Age.
    /// </summary>
    public int CalculatedAge
    {
        get
        {
            if (DateOfBirth.HasValue)
            {
                var today = DateTime.Today;
                var age = today.Year - DateOfBirth.Value.Year;
                if (DateOfBirth.Value.Date > today.AddYears(-age)) age--;
                return Math.Max(0, age);
            }
            return Age ?? 0;
        }
    }
}
