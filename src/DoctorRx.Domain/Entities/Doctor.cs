using System.Collections.Generic;
using DoctorRx.Domain.Common;

namespace DoctorRx.Domain.Entities;

/// <summary>
/// Represents the prescribing doctor profile and clinic credentials.
/// </summary>
public class Doctor : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }

    // Clinic / Practice Information
    public string ClinicName { get; set; } = string.Empty;
    public string? ClinicAddress { get; set; }
    public string? ClinicPhone { get; set; }
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}
